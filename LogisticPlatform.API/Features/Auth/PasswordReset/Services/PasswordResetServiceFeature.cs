using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using LogisticPlatform.API.Features.Auth.PasswordReset.Schemas;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Features.Auth.PasswordReset.Services;

internal sealed class PasswordResetServiceFeature(
    AppDbContext context,
    IPasswordResetService passwordResetCryptoService,
    IPasswordHashService passwordHashService,
    TimeProvider timeProvider) : IPasswordResetServiceFeature
{
    public async Task<ResultSchema<bool>> RequestResetAsync(PasswordResetRequestSchema request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ResultSchema<bool>.Failure("Email cannot be empty.");
        }

        var normalizedEmail = request.Email.ToUpperInvariant();
        var legacyEmail = new string([.. request.Email.Select(char.ToLowerInvariant)]);
        var user = await context.Users.FirstOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail || candidate.Email == legacyEmail,
            cancellationToken);

        if (user is null)
        {
            return ResultSchema<bool>.Success(true);
        }

        await passwordResetCryptoService.GenerateAndSendResetTokenAsync(user.Id, user.Email, user.Name, cancellationToken);
        return ResultSchema<bool>.Success(true);
    }

    public async Task<ResultSchema<bool>> ResetPasswordAsync(PasswordResetExecuteSchema request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ResultSchema<bool>.Failure("Token parameters or new password credentials cannot be empty.");
        }

        var tokenHash = passwordResetCryptoService.ComputeSha256Hash(request.Token);
        var tokenEntity = await context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (tokenEntity is null || tokenEntity.IsConsumed || tokenEntity.ExpiresAt <= now)
        {
            return ResultSchema<bool>.Failure("The provided recovery token is invalid, expired, or has already been consumed.");
        }

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == tokenEntity.UserId, cancellationToken);
        if (user is null)
        {
            return ResultSchema<bool>.Failure("User reference associated with recovery token not found.");
        }

        user.SetPasswordHash(passwordHashService.HashPassword(user, request.NewPassword));

        var outstandingResetTokens = await context.PasswordResetTokens
            .Where(token => token.UserId == user.Id && !token.IsConsumed)
            .ToListAsync(cancellationToken);
        foreach (var resetToken in outstandingResetTokens)
        {
            resetToken.IsConsumed = true;
            resetToken.IsDeleted = true;
            resetToken.DeletedAt = now;
        }

        var activeSessions = await context.UserDeviceSessions
            .Where(s => s.UserId == user.Id && s.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.IsDeleted = true;
            session.DeletedAt = now;
        }

        var activeRefreshTokens = await context.RefreshTokenSessions
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeRefreshTokens)
        {
            token.IsRevoked = true;
        }

        await context.SaveChangesAsync(cancellationToken);
        return ResultSchema<bool>.Success(true);
    }
}
