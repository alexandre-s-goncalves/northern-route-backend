using System.Security.Cryptography;
using System.Text;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Mfa.Schemas;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.Mfa;

public sealed class MfaServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly FakeEmailService _emailService;
    private readonly TestTimeProvider _timeProvider;
    private readonly MfaService _service;

    public MfaServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _emailService = new FakeEmailService();
        _timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("MFA_CODE_PEPPER", _testPepper)])
            .Build();
        _service = new MfaService(_context, _emailService, configuration, _timeProvider);
    }

    private const string _testPepper = "MfaTestPepper_2026_AtLeast32Bytes";

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task SendEmailCodeAsync_ShouldFail_WhenUserDoesNotExist()
    {
        var result = await _service.SendEmailCodeAsync(Guid.NewGuid(), null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User profile reference not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailCodeAsync_ShouldCreateConfiguration_AndMaskLongEmail()
    {
        var user = await AddUserAsync("alice@example.com");
        _emailService.OnSend = async () =>
            Assert.Empty(await _context.MfaConfigurations.AsNoTracking().ToListAsync());

        var result = await _service.SendEmailCodeAsync(user.Id, null, CancellationToken.None);

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
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15), configuration.CodeExpiresAt);

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
            () => _service.SendEmailCodeAsync(user.Id, null, CancellationToken.None));

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

        var result = await _service.SendEmailCodeAsync(user.Id, null, CancellationToken.None);

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
            new MfaVerificationRequestSchema("123456", Guid.NewGuid(), null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MFA via email security policy is not enabled for this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenConfigurationIsDisabled()
    {
        var user = await AddUserAsync("disabled@example.com");
        var configuration = await AddConfigurationAsync(user.Id, isEnabled: false, provider: "email", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id, configuration.ChallengeDeviceSessionId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MFA via email security policy is not enabled for this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenProviderIsNotEmail()
    {
        var user = await AddUserAsync("totp@example.com");
        var configuration = await AddConfigurationAsync(user.Id, isEnabled: true, provider: "totp", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id, configuration.ChallengeDeviceSessionId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MFA via email security policy is not enabled for this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenCodeIsEmpty()
    {
        var user = await AddUserAsync("empty-code@example.com");
        var configuration = await AddConfigurationAsync(user.Id, isEnabled: true, provider: "email", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema(" ", user.Id, configuration.ChallengeDeviceSessionId),
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
            codeExpiresAt: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id, configuration.ChallengeDeviceSessionId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Security token verification code has expired.", result.ErrorMessage);
        Assert.False(configuration.IsDeleted);
        Assert.Empty(configuration.SecretKeyHash);
        Assert.Null(configuration.CodeExpiresAt);
        Assert.Single(await _context.MfaConfigurations.ToListAsync());
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldFail_WhenCodeDoesNotMatch()
    {
        var user = await AddUserAsync("mismatch@example.com");
        var configuration = await AddConfigurationAsync(user.Id, isEnabled: true, provider: "email", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("654321", user.Id, configuration.ChallengeDeviceSessionId),
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
            codeExpiresAt: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id, configuration.ChallengeDeviceSessionId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.False(configuration.IsDeleted);
        Assert.Empty(configuration.SecretKeyHash);
        Assert.Null(configuration.CodeExpiresAt);
        Assert.Single(await _context.MfaConfigurations.ToListAsync());
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldExpireAtExactBoundary()
    {
        var user = await AddUserAsync("boundary@example.com");
        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15);
        var configuration = await AddConfigurationAsync(
            user.Id,
            isEnabled: true,
            provider: "email",
            ComputeHash("123456"),
            codeExpiresAt: expiresAt);
        _timeProvider.SetUtcNow(new DateTimeOffset(expiresAt, TimeSpan.Zero));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id, configuration.ChallengeDeviceSessionId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Security token verification code has expired.", result.ErrorMessage);
        Assert.True(configuration.IsEnabled);
        Assert.False(configuration.IsDeleted);
        Assert.Empty(configuration.SecretKeyHash);
    }

    [Fact]
    public async Task VerifyMfaAsync_ShouldRejectChallengeForAnotherDevice()
    {
        var user = await AddUserAsync("wrong-device@example.com");
        var configuration = await AddConfigurationAsync(user.Id, isEnabled: true, provider: "email", ComputeHash("123456"));

        var result = await _service.VerifyMfaAsync(
            new MfaVerificationRequestSchema("123456", user.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Security token verification code mismatch.", result.ErrorMessage);
        Assert.False(string.IsNullOrEmpty(configuration.SecretKeyHash));
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
        DateTime? updatedAt = null,
        DateTime? codeExpiresAt = null)
    {
        var configuration = new MfaConfiguration
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            IsEnabled = isEnabled,
            Provider = provider,
            SecretKeyHash = secretKeyHash,
            CodeExpiresAt = codeExpiresAt ?? _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15),
            ChallengeDeviceSessionId = Guid.NewGuid(),
            UpdatedAt = updatedAt ?? _timeProvider.GetUtcNow().UtcDateTime,
            UserId = userId
        };
        _context.MfaConfigurations.Add(configuration);
        await _context.SaveChangesAsync();
        return configuration;
    }

    private static string ComputeHash(string code)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_testPepper), Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash);
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
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

        public Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            return Task.CompletedTask;
        }
    }
}
