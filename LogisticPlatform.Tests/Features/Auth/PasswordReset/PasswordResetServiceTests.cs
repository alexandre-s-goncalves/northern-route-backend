using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.PasswordReset;

public sealed class PasswordResetServiceTests
{
    [Fact]
    public void ComputeSha256Hash_ShouldReturnLowercaseSha256Hex()
    {
        using var context = CreateContext();
        var service = CreateService(context, new FakeEmailService(), new FixedTimeProvider());

        var actualHash = service.ComputeSha256Hash("reset-token");
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("reset-token")));

        Assert.Equal(expectedHash, actualHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ComputeSha256Hash_ShouldRejectBlankInput(string? rawData)
    {
        using var context = CreateContext();
        var service = CreateService(context, new FakeEmailService(), new FixedTimeProvider());

        Assert.ThrowsAny<ArgumentException>(() => service.ComputeSha256Hash(rawData!));
    }

    [Fact]
    public async Task GenerateAndSendResetTokenAsync_ShouldPersistHashAndEmailResetLink()
    {
        using var context = CreateContext();
        var emailService = new FakeEmailService();
        var timeProvider = new FixedTimeProvider();
        var service = CreateService(context, emailService, timeProvider);
        var userId = Guid.NewGuid();

        var rawToken = await service.GenerateAndSendResetTokenAsync(
            userId,
            "user@example.com",
            "Test User",
            default);

        var resetToken = await context.PasswordResetTokens.SingleAsync();
        Assert.Equal(userId, resetToken.UserId);
        Assert.Equal(service.ComputeSha256Hash(rawToken), resetToken.TokenHash);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15), resetToken.ExpiresAt);
        Assert.False(resetToken.IsConsumed);
        Assert.Equal("user@example.com", emailService.ToEmail);
        Assert.Equal("Test User", emailService.UserName);
        Assert.Equal($"http://localhost:5173/reset-password?token={rawToken}", emailService.ResetLink);
        Assert.Empty(emailService.MfaMessages);
    }

    [Fact]
    public async Task GenerateAndSendResetTokenAsync_ShouldUseFirstConfiguredFrontendOrigin()
    {
        using var context = CreateContext();
        var emailService = new FakeEmailService();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALLOWED_ORIGINS"] = " https://app.example.com/,https://other.example.com "
            })
            .Build();
        var service = new PasswordResetService(context, emailService, new FixedTimeProvider(), configuration);

        var rawToken = await service.GenerateAndSendResetTokenAsync(
            Guid.NewGuid(),
            "user@example.com",
            "Test User",
            default);

        Assert.Equal($"https://app.example.com/reset-password?token={rawToken}", emailService.ResetLink);
    }

    [Fact]
    public async Task GenerateAndSendResetTokenAsync_ShouldRejectBlankEmail()
    {
        using var context = CreateContext();
        var service = CreateService(context, new FakeEmailService(), new FixedTimeProvider());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAndSendResetTokenAsync(Guid.NewGuid(), " ", "Test User", default));
    }

    [Fact]
    public async Task GenerateAndSendResetTokenAsync_ShouldRejectBlankUserName()
    {
        using var context = CreateContext();
        var service = CreateService(context, new FakeEmailService(), new FixedTimeProvider());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAndSendResetTokenAsync(Guid.NewGuid(), "user@example.com", " ", default));
    }

    private static AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static PasswordResetService CreateService(
        AppDbContext context,
        IEmailService emailService,
        TimeProvider timeProvider)
    {
        return new PasswordResetService(context, emailService, timeProvider, new ConfigurationBuilder().Build());
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string ToEmail, string UserName, string SecurityCode)> MfaMessages { get; } = [];
        public string? ToEmail { get; private set; }
        public string? UserName { get; private set; }
        public string? ResetLink { get; private set; }

        Task IEmailService.SendMfaCodeEmailAsync(string toEmail, string userName, string securityCode)
        {
            MfaMessages.Add((toEmail, userName, securityCode));
            return Task.CompletedTask;
        }

        Task IEmailService.SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            ToEmail = toEmail;
            UserName = userName;
            ResetLink = resetLink;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
