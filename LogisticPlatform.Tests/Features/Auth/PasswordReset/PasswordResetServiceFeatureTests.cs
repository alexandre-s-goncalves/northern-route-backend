using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using LogisticPlatform.API.Features.Auth.PasswordReset.Schemas;
using LogisticPlatform.API.Features.Auth.PasswordReset.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.PasswordReset;

public sealed class PasswordResetServiceFeatureTests
{
    private static readonly DateTimeOffset _currentTime = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RequestResetAsync_ShouldThrow_WhenRequestIsNull()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RequestResetAsync(null!, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RequestResetAsync_ShouldFail_WhenEmailIsBlank(string? email)
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.RequestResetAsync(new PasswordResetRequestSchema(email!), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Email cannot be empty.", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestResetAsync_ShouldReturnSuccessWithoutSending_WhenUserDoesNotExist()
    {
        await using var context = CreateContext();
        var cryptoService = new FakePasswordResetCryptoService();
        var service = CreateService(context, cryptoService);

        var result = await service.RequestResetAsync(new PasswordResetRequestSchema("missing@example.com"), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal(0, cryptoService.GenerateCalls);
    }

    [Fact]
    public async Task RequestResetAsync_ShouldGenerateToken_ForLegacyLowercaseEmail()
    {
        await using var context = CreateContext();
        var user = new User("Reset User", "reset@example.com", "old-hash", Guid.NewGuid());
        context.Users.Add(user);
        context.Entry(user).Property(candidate => candidate.Email).CurrentValue = "reset@example.com";
        await context.SaveChangesAsync();
        var cryptoService = new FakePasswordResetCryptoService();
        var service = CreateService(context, cryptoService);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await service.RequestResetAsync(
            new PasswordResetRequestSchema("RESET@example.com"),
            cancellationTokenSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, cryptoService.GenerateCalls);
        Assert.Equal(user.Id, cryptoService.GeneratedForUserId);
        Assert.Equal("reset@example.com", cryptoService.GeneratedForEmail);
        Assert.Equal("Reset User", cryptoService.GeneratedForUserName);
        Assert.Equal(cancellationTokenSource.Token, cryptoService.GeneratedWithCancellationToken);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldThrow_WhenRequestIsNull()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ResetPasswordAsync(null!, default));
    }

    [Theory]
    [InlineData("", "NewPassword123")]
    [InlineData("reset-token", " ")]
    public async Task ResetPasswordAsync_ShouldFail_WhenTokenOrPasswordIsBlank(string token, string newPassword)
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync(new PasswordResetExecuteSchema(newPassword, token), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Token parameters or new password credentials cannot be empty.", result.ErrorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenTokenDoesNotExist()
    {
        await using var context = CreateContext();
        var cryptoService = new FakePasswordResetCryptoService();
        var service = CreateService(context, cryptoService);

        var result = await service.ResetPasswordAsync(new PasswordResetExecuteSchema("NewPassword123", "missing"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("The provided recovery token is invalid, expired, or has already been consumed.", result.ErrorMessage);
        Assert.Equal("missing", Assert.Single(cryptoService.HashedInputs));
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenTokenWasConsumed()
    {
        await using var context = CreateContext();
        var user = CreateUser();
        var token = CreateToken(user.Id, "consumed", _currentTime.UtcDateTime.AddMinutes(10));
        token.IsConsumed = true;
        context.Users.Add(user);
        context.PasswordResetTokens.Add(token);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync(new PasswordResetExecuteSchema("NewPassword123", "consumed"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("The provided recovery token is invalid, expired, or has already been consumed.", result.ErrorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenTokenHasExpired()
    {
        await using var context = CreateContext();
        var user = CreateUser();
        context.Users.Add(user);
        context.PasswordResetTokens.Add(CreateToken(user.Id, "expired", _currentTime.UtcDateTime));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync(new PasswordResetExecuteSchema("NewPassword123", "expired"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("The provided recovery token is invalid, expired, or has already been consumed.", result.ErrorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenTokenUserDoesNotExist()
    {
        await using var context = CreateContext();
        context.PasswordResetTokens.Add(CreateToken(Guid.NewGuid(), "orphan", _currentTime.UtcDateTime.AddMinutes(10)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync(new PasswordResetExecuteSchema("NewPassword123", "orphan"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("User reference associated with recovery token not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldHashPasswordAndRevokeOutstandingTokensAndSessions()
    {
        await using var context = CreateContext();
        var user = CreateUser();
        var activeSession = CreateSession(user.Id, true);
        var inactiveSession = CreateSession(user.Id, false);
        var activeRefreshToken = CreateRefreshToken(user.Id, activeSession.Id, false);
        var alreadyRevokedToken = CreateRefreshToken(user.Id, activeSession.Id, true);
        var currentResetToken = CreateToken(user.Id, "current", _currentTime.UtcDateTime.AddMinutes(10));
        var otherResetToken = CreateToken(user.Id, "other", _currentTime.UtcDateTime.AddMinutes(20));
        context.Users.Add(user);
        context.UserDeviceSessions.AddRange(activeSession, inactiveSession);
        context.RefreshTokenSessions.AddRange(activeRefreshToken, alreadyRevokedToken);
        context.PasswordResetTokens.AddRange(currentResetToken, otherResetToken);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync(new PasswordResetExecuteSchema("NewPassword123", "current"), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, "NewPassword123"));

        var persistedResetTokens = await context.PasswordResetTokens.IgnoreQueryFilters().ToListAsync();
        Assert.All(persistedResetTokens, token =>
        {
            Assert.True(token.IsConsumed);
            Assert.True(token.IsDeleted);
            Assert.Equal(_currentTime.UtcDateTime, token.DeletedAt);
        });

        var persistedSessions = await context.UserDeviceSessions.IgnoreQueryFilters().ToListAsync();
        Assert.False(activeSession.IsActive);
        Assert.True(activeSession.IsDeleted);
        Assert.Equal(_currentTime.UtcDateTime, activeSession.DeletedAt);
        Assert.False(inactiveSession.IsActive);
        Assert.False(inactiveSession.IsDeleted);
        Assert.Equal(2, persistedSessions.Count);

        Assert.True(activeRefreshToken.IsRevoked);
        Assert.True(alreadyRevokedToken.IsRevoked);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldSucceed_WhenNoSessionsOrOtherTokensExist()
    {
        await using var context = CreateContext();
        var user = CreateUser();
        context.Users.Add(user);
        context.PasswordResetTokens.Add(CreateToken(user.Id, "only-token", _currentTime.UtcDateTime.AddMinutes(10)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync(new PasswordResetExecuteSchema("NewPassword123", "only-token"), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    private static AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static PasswordResetServiceFeature CreateService(
        AppDbContext context,
        FakePasswordResetCryptoService? cryptoService = null)
    {
        return new PasswordResetServiceFeature(
            context,
            cryptoService ?? new FakePasswordResetCryptoService(),
            new PasswordHashService(new PasswordHasher<User>()),
            new FixedTimeProvider());
    }

    private static User CreateUser()
    {
        return new User("Reset User", $"reset-{Guid.NewGuid():N}@example.com", "old-hash", Guid.NewGuid());
    }

    private static PasswordResetToken CreateToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        return new PasswordResetToken
        {
            ExpiresAt = expiresAt,
            TokenHash = $"hash:{tokenHash}",
            UserId = userId
        };
    }

    private static UserDeviceSession CreateSession(Guid userId, bool isActive)
    {
        return new UserDeviceSession
        {
            BrowserName = "Test",
            BrowserVersion = "1",
            DeviceModel = "Test",
            DeviceType = "desktop",
            IpAddress = "127.0.0.1",
            IsActive = isActive,
            LastActiveAt = _currentTime.UtcDateTime,
            LocationCity = "UNKNOWN",
            LocationCountry = "UNKNOWN",
            OsName = "Test OS",
            OsVersion = "1",
            UserId = userId
        };
    }

    private static RefreshTokenSession CreateRefreshToken(Guid userId, Guid sessionId, bool isRevoked)
    {
        return new RefreshTokenSession
        {
            DeviceSessionId = sessionId,
            ExpiresAt = _currentTime.UtcDateTime.AddHours(1),
            IsRevoked = isRevoked,
            TokenHash = Guid.NewGuid().ToString("N"),
            UserId = userId
        };
    }

    private sealed class FakePasswordResetCryptoService : IPasswordResetService
    {
        public List<string> HashedInputs { get; } = [];
        public int GenerateCalls { get; private set; }
        public Guid? GeneratedForUserId { get; private set; }
        public string? GeneratedForEmail { get; private set; }
        public string? GeneratedForUserName { get; private set; }
        public CancellationToken GeneratedWithCancellationToken { get; private set; }

        string IPasswordResetService.ComputeSha256Hash(string rawData)
        {
            HashedInputs.Add(rawData);
            return $"hash:{rawData}";
        }

        Task<string> IPasswordResetService.GenerateAndSendResetTokenAsync(
            Guid userId,
            string userEmail,
            string userName,
            CancellationToken cancellationToken)
        {
            GenerateCalls++;
            GeneratedForUserId = userId;
            GeneratedForEmail = userEmail;
            GeneratedForUserName = userName;
            GeneratedWithCancellationToken = cancellationToken;
            return Task.FromResult("generated-token");
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _currentTime;
    }
}
