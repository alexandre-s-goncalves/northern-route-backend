using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Common.Security;

internal sealed class RefreshTokenService(AppDbContext context, TimeProvider timeProvider) : IRefreshTokenService
{
    public async Task<string> CreateTokenAsync(Guid userId, Guid deviceSessionId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        var refreshToken = Convert.ToBase64String(randomNumber);
        var tokenHash = ComputeSha256Hash(refreshToken);

        var session = new RefreshTokenSession
        {
            CreatedAt = now,
            DeviceSessionId = deviceSessionId,
            ExpiresAt = now.AddDays(7),
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
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = await context.RefreshTokenSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (session is null || session.IsRevoked || session.ExpiresAt <= now)
        {
            return null;
        }

        if (context.Database.IsRelational())
        {
            var updatedRows = await context.RefreshTokenSessions
                .Where(candidate =>
                    candidate.Id == session.Id &&
                    candidate.TokenHash == tokenHash &&
                    !candidate.IsRevoked &&
                    candidate.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.IsRevoked, true)
                    .SetProperty(candidate => candidate.UpdatedAt, now),
                    cancellationToken);

            if (updatedRows != 1)
            {
                return null;
            }

            session.IsRevoked = true;
            session.UpdatedAt = now;
            return session;
        }

        var trackedSession = await context.RefreshTokenSessions
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == session.Id &&
                !candidate.IsRevoked &&
                candidate.ExpiresAt > now,
                cancellationToken);

        if (trackedSession is null)
        {
            return null;
        }

        trackedSession.IsRevoked = true;
        trackedSession.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return trackedSession;
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
