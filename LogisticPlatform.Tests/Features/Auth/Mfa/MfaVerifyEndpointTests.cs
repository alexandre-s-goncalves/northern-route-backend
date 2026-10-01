using System.Net;
using System.Net.Http.Json;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using LogisticPlatform.API.Features.Auth.Mfa.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.Mfa;

public sealed class MfaVerifyEndpointTests : IClassFixture<WebTestFixture>
{
    private readonly HttpClient _client;
    private readonly WebTestFixture _factory;

    public MfaVerifyEndpointTests(WebTestFixture factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Verify_ShouldReturnTokens_AndRetainEnrollment_WhenChallengeIsValid()
    {
        var (user, pendingLogin, code) = await CreatePendingLoginAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerificationRequestSchema(code, user.Id, pendingLogin.DeviceSessionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponseSchema>();
        Assert.NotNull(body);
        Assert.False(body.IsMfaRequired);
        Assert.Equal(user.Id, body.UserId);
        Assert.Equal(pendingLogin.DeviceSessionId, body.DeviceSessionId);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = await context.MfaConfigurations.SingleAsync(config => config.UserId == user.Id);
        Assert.True(configuration.IsEnabled);
        Assert.False(configuration.IsDeleted);
        Assert.Empty(configuration.SecretKeyHash);
        Assert.Null(configuration.CodeExpiresAt);
        Assert.Null(configuration.ChallengeDeviceSessionId);

        var sessions = await context.RefreshTokenSessions.Where(session => session.UserId == user.Id).ToListAsync();
        var session = Assert.Single(sessions);
        Assert.Equal(body.DeviceSessionId, session.DeviceSessionId);
        Assert.False(session.IsRevoked);

        var audits = await context.LoginAudits.Where(audit => audit.UserId == user.Id).ToListAsync();
        Assert.Contains(audits, audit => audit.Status == "MFA_PENDING");
        Assert.Contains(audits, audit => audit.Status == "SUCCESS");
    }

    [Fact]
    public async Task Verify_ShouldRejectRequest_WhenDeviceSessionIsMissing()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerificationRequestSchema("123456", Guid.NewGuid(), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verify_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerificationRequestSchema("123456", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Verify_ShouldRejectRequest_WhenDeviceSessionDoesNotBelongToUser()
    {
        var user = await CreateUserAsync($"missing-device-{Guid.NewGuid():N}@example.com");

        var response = await _client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerificationRequestSchema("123456", user.Id, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verify_ShouldRejectInvalidCode_WithoutConsumingChallenge()
    {
        var (user, pendingLogin, _) = await CreatePendingLoginAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerificationRequestSchema("000000", user.Id, pendingLogin.DeviceSessionId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = await context.MfaConfigurations.SingleAsync(config => config.UserId == user.Id);
        Assert.True(configuration.IsEnabled);
        Assert.False(string.IsNullOrEmpty(configuration.SecretKeyHash));
        Assert.Null(await context.RefreshTokenSessions.FirstOrDefaultAsync(session => session.UserId == user.Id));
    }

    [Fact]
    public async Task Verify_ShouldRejectWhenMfaIsNotEnabled()
    {
        var user = await CreateUserAsync($"no-mfa-{Guid.NewGuid():N}@example.com");
        var device = await CreateDeviceSessionAsync(user.Id);

        var response = await _client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerificationRequestSchema("123456", user.Id, device.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(User User, LoginResponseSchema PendingLogin, string Code)> CreatePendingLoginAsync()
    {
        var email = $"mfa-{Guid.NewGuid():N}@example.com";
        var user = await CreateUserAsync(email);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.MfaConfigurations.Add(new MfaConfiguration
            {
                Id = Guid.NewGuid(),
                IsEnabled = true,
                Provider = "email",
                SecretKeyHash = string.Empty,
                UserId = user.Id
            });
            await context.SaveChangesAsync();
        }

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestSchema(email, "SecurePassword123"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var result = await loginResponse.Content.ReadFromJsonAsync<LogisticPlatform.API.Common.ResultSchema<LoginResponseSchema>>();
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsMfaRequired);
        Assert.Empty(result.Data.Token);
        Assert.Empty(result.Data.RefreshToken);

        var (UserName, SecurityCode) = _factory.Services.GetRequiredService<TestEmailService>().GetLastMessage(user.Email);
        Assert.Equal(user.Name, UserName);
        return (user, result.Data, SecurityCode);
    }

    private async Task<User> CreateUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new Role($"ROLE_{Guid.NewGuid():N}");
        var user = new User("MFA Endpoint User", email, "SecurePassword123", role.Id);
        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<UserDeviceSession> CreateDeviceSessionAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = new UserDeviceSession
        {
            BrowserName = "Test",
            BrowserVersion = "1",
            DeviceModel = "Test device",
            DeviceType = "desktop",
            Id = Guid.NewGuid(),
            IpAddress = "127.0.0.1",
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            LocationCity = "UNKNOWN",
            LocationCountry = "UNKNOWN",
            OsName = "Test OS",
            OsVersion = "1",
            UserId = userId
        };
        context.UserDeviceSessions.Add(device);
        await context.SaveChangesAsync();
        return device;
    }
}
