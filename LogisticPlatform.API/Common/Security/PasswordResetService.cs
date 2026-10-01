using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;

namespace LogisticPlatform.API.Common.Security;

internal sealed class PasswordResetService(
    AppDbContext context,
    IEmailService emailService) : IPasswordResetService
{
    public string ComputeSha256Hash(string rawData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawData);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexStringLower(bytes);
    }

    public async Task<string> GenerateAndSendResetTokenAsync(Guid userId, string userEmail, string userName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var tokenBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);

        var rawToken = Convert.ToHexStringLower(tokenBytes);
        var tokenHash = ComputeSha256Hash(rawToken);

        var resetTokenEntity = new PasswordResetToken
        {
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Id = Guid.NewGuid(),
            IsConsumed = false,
            TokenHash = tokenHash,
            UserId = userId
        };

        context.PasswordResetTokens.Add(resetTokenEntity);
        await context.SaveChangesAsync(cancellationToken);

        var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "http://localhost:5173";
        var resetLink = $"{allowedOrigins}/reset-password?token={rawToken}";

        await emailService.SendMfaCodeEmailAsync(userEmail, userName, $"LINK:{resetLink}");

        return rawToken;
    }
}
