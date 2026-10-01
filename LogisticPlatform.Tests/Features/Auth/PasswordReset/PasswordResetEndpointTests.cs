using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Features.Auth.PasswordReset;
using LogisticPlatform.API.Features.Auth.PasswordReset.Schemas;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.PasswordReset;

public sealed class PasswordResetEndpointTests : IClassFixture<WebTestFixture>
{
    private readonly HttpClient _client;
    private readonly WebTestFixture _factory;

    public PasswordResetEndpointTests(WebTestFixture factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RequestReset_ShouldReturnBadRequest_WhenEmailIsBlank()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/password-reset/request",
            new PasswordResetRequestSchema(" "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestReset_ShouldReturnOk_WhenUserDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/password-reset/request",
            new PasswordResetRequestSchema($"missing-{Guid.NewGuid():N}@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ShouldRejectInvalidToken_ThenCompleteResetWithValidToken()
    {
        var user = await CreateUserAsync();
        var requestResponse = await _client.PostAsJsonAsync(
            "/api/auth/password-reset/request",
            new PasswordResetRequestSchema(new string([.. user.Email.Select(char.ToLowerInvariant)])));

        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var emailService = _factory.Services.GetRequiredService<TestEmailService>();
        var resetLink = emailService.GetLastPasswordResetLink(user.Email);
        var rawToken = resetLink[(resetLink.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..];
        var invalidResponse = await _client.PostAsJsonAsync(
            "/api/auth/password-reset/reset",
            new PasswordResetExecuteSchema("NewPassword123", "invalid-token"));

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var resetResponse = await _client.PostAsJsonAsync(
            "/api/auth/password-reset/reset",
            new PasswordResetExecuteSchema("NewPassword123", rawToken));

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(updatedUser, updatedUser.PasswordHash, "NewPassword123"));
    }

    [Fact]
    public void PasswordResetModule_ShouldThrow_WhenEndpointBuilderIsNull()
    {
        var module = new PasswordResetModule();

        Assert.Throws<ArgumentNullException>(() => module.MapEndpoints(null!));
    }

    private async Task<User> CreateUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new Role($"RESET_{Guid.NewGuid():N}");
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        var user = new User(
            "Password Reset User",
            $"reset-{Guid.NewGuid():N}@example.com",
            string.Empty,
            role.Id);
        user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, "CurrentPassword123"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
