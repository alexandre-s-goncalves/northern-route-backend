using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Common.Security;

internal sealed class RefreshTokenService(AppDbContext context) : IRefreshTokenService
{
    public async Task<string> CreateTokenAsync(Guid userId, Guid deviceSessionId, CancellationToken cancellationToken)
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        var refreshToken = Convert.ToBase64String(randomNumber);
        var tokenHash = ComputeSha256Hash(refreshToken);

        var session = new RefreshTokenSession
        {
            CreatedAt = DateTime.UtcNow,
            DeviceSessionId = deviceSessionId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Id = Guid.NewGuid(),
            IsRevoked = false,
            TokenHash = tokenHash,
            UserId = userId
        };

        context.RefreshTokenSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        return refreshToken;
    }

    public async Task<RefreshTokenSession?> ValidateAndRotateTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var tokenHash = ComputeSha256Hash(token);
        var session = await context.RefreshTokenSessions
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (session is null || session.IsRevoked || session.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        session.IsRevoked = true;
        session.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return session;
    }

    private static string ComputeSha256Hash(string rawData)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }
}
