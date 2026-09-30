using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Mfa.Schemas;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.Mfa;

public sealed class MfaServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly FakeEmailService _emailService;
    private readonly MfaService _service;

    public MfaServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _emailService = new FakeEmailService();
        _service = new MfaService(_context, _emailService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task SendEmailCodeAsync_ShouldFail_WhenUserDoesNotExist()
    {
        var result = await _service.SendEmailCodeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User profile reference not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailCodeAsync_ShouldCreateConfiguration_AndMaskLongEmail()
    {
        var user = await AddUserAsync("alice@example.com");
        _emailService.OnSend = async () =>
            Assert.Empty(await _context.MfaConfigurations.AsNoTracking().ToListAsync());

        var result = await _service.SendEmailCodeAsync(user.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("AL***@EXAMPLE.COM", result.Data.MaskedEmail);
        Assert.True(result.Data.MessageDispatched);
        Assert.Equal(user.Id, result.Data.UserId);

        var configuration = await _context.MfaConfigurations.SingleAsync();
        Assert.True(configuration.IsEnabled);
        Assert.Equal("email", configuration.Provider);
        Assert.Equal(64, configuration.SecretKeyHash.Length);
        Assert.NotNull(configuration.UpdatedAt);

        var (toEmail, userName, securityCode) = Assert.Single(_emailService.SentMessages);
        Assert.Equal(user.Email, toEmail);
        Assert.Equal(user.Name, userName);
        Assert.Matches("^[0-9]{6}$", securityCode);
        Assert.Equal(ComputeHash(securityCode), configuration.SecretKeyHash);
    }

    [Fact]
    public async Task SendEmailCodeAsync_ShouldNotPersistConfiguration_WhenEmailDispatchFails()
    {
        var user = await AddUserAsync("email-failure@example.com");
        _emailService.ExceptionToThrow = new InvalidOperationException("SMTP unavailable");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendEmailCodeAsync(user.Id, CancellationToken.None));

        Assert.Equal("SMTP unavailable", exception.Message);
        Assert.Empty(await _context.MfaConfigurations.AsNoTracking().ToListAsync());
        Assert.Empty(_emailService.SentMessages);
    }

    [Fact]
    public async Task SendEmailCodeAsync_ShouldUpdateConfiguration_AndMaskShortEmail()
    {
        var user = await AddUserAsync("ab@example.com");
        var configuration = new MfaConfiguration
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            IsEnabled = false,
            Provider = "totp",
            SecretKeyHash = "old-hash",
            UserId = user.Id
        };
        _context.MfaConfigurations.Add(configuration);
        await _context.SaveChangesAsync();

        var result = await _service.SendEmailCodeAsync(user.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("***@EXAMPLE.COM", result.Data.MaskedEmail);
        Assert.True(configuration.IsEnabled);
        Assert.Equal("email", configuration.Provider);
        Assert.NotEqual("old-hash", configuration.SecretKeyHash);
        Assert.NotNull(configuration.UpdatedAt);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldThrow_WhenRequestIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.VerifyMfaAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenConfigurationDoesNotExist()
    {
        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MFA via email security policy is not enabled for this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenConfigurationIsDisabled()
    {
        var user = await AddUserAsync("disabled@example.com");
        await AddConfigurationAsync(user.Id, isEnabled: false, provider: "email", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MFA via email security policy is not enabled for this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenProviderIsNotEmail()
    {
        var user = await AddUserAsync("totp@example.com");
        await AddConfigurationAsync(user.Id, isEnabled: true, provider: "totp", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MFA via email security policy is not enabled for this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenCodeIsEmpty()
    {
        var user = await AddUserAsync("empty-code@example.com");
        await AddConfigurationAsync(user.Id, isEnabled: true, provider: "email", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema(" ", user.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Verification digits code parameter cannot be empty.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFailAndDeleteConfiguration_WhenCodeHasExpired()
    {
        var user = await AddUserAsync("expired@example.com");
        var configuration = await AddConfigurationAsync(
            user.Id,
            isEnabled: true,
            provider: "email",
            ComputeHash("123456"),
            updatedAt: DateTime.UtcNow.AddMinutes(-16));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Security token verification code has expired.", result.ErrorMessage);
        Assert.True(configuration.IsDeleted);
        Assert.NotNull(configuration.DeletedAt);
        Assert.Empty(await _context.MfaConfigurations.ToListAsync());
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenCodeDoesNotMatch()
    {
        var user = await AddUserAsync("mismatch@example.com");
        await AddConfigurationAsync(user.Id, isEnabled: true, provider: "email", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("654321", user.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Security token verification code mismatch.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldSucceedAndDeleteConfiguration_WhenCodeMatches()
    {
        var user = await AddUserAsync("verified@example.com");
        var configuration = await AddConfigurationAsync(
            user.Id,
            isEnabled: true,
            provider: "email",
            ComputeHash("123456"),
            updatedAt: null);

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.True(configuration.IsDeleted);
        Assert.NotNull(configuration.DeletedAt);
        Assert.Empty(await _context.MfaConfigurations.ToListAsync());
    }

    private async Task<User> AddUserAsync(string email)
    {
        var role = new Role($"ROLE_{Guid.NewGuid():N}");
        var user = new User("MFA Test User", email, "Password123", role.Id);
        _context.Roles.Add(role);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<MfaConfiguration> AddConfigurationAsync(
        Guid userId,
        bool isEnabled,
        string provider,
        string secretKeyHash,
        DateTime? updatedAt = null)
    {
        var configuration = new MfaConfiguration
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            IsEnabled = isEnabled,
            Provider = provider,
            SecretKeyHash = secretKeyHash,
            UpdatedAt = updatedAt,
            UserId = userId
        };
        _context.MfaConfigurations.Add(configuration);
        await _context.SaveChangesAsync();
        return configuration;
    }

    private static string ComputeHash(string code)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return string.Concat(hash.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string ToEmail, string UserName, string SecurityCode)> SentMessages { get; } = [];
        public Func<Task>? OnSend { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public async Task SendMfaCodeEmailAsync(string toEmail, string userName, string securityCode)
        {
            if (OnSend is not null)
            {
                await OnSend();
            }

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            SentMessages.Add((toEmail, userName, securityCode));
        }
    }
}
