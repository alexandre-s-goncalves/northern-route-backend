using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security.Contracts;
using Microsoft.Extensions.Configuration;

namespace LogisticPlatform.API.Common.Security;

internal sealed class PasswordResetService(
    AppDbContext context,
    IEmailService emailService,
    TimeProvider timeProvider,
    IConfiguration configuration) : IPasswordResetService
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
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var resetTokenEntity = new PasswordResetToken
        {
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15),
            Id = Guid.NewGuid(),
            IsConsumed = false,
            TokenHash = tokenHash,
            UserId = userId
        };

        context.PasswordResetTokens.Add(resetTokenEntity);
        await context.SaveChangesAsync(cancellationToken);

        var allowedOrigins = configuration["ALLOWED_ORIGINS"];
        var frontendOrigin = allowedOrigins?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(origin => origin.Trim().TrimEnd('/'))
            .FirstOrDefault(origin => origin.Length > 0)
            ?? "http://localhost:5173";
        var resetLink = $"{frontendOrigin}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        await emailService.SendPasswordResetEmailAsync(userEmail, userName, resetLink);

        return rawToken;
    }
}
