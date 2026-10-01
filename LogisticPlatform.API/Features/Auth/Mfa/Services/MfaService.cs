using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using LogisticPlatform.API.Features.Auth.Mfa.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LogisticPlatform.API.Features.Auth.Mfa.Services;

internal sealed class MfaService(
    AppDbContext context,
    IEmailService emailService,
    IConfiguration configuration,
    TimeProvider timeProvider) : IMfaService
{
    private const string _emailProvider = "email";

    public async Task<ResultSchema<MfaEmailResponseSchema>> SendEmailCodeAsync(
        Guid userId,
        Guid? deviceSessionId,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ResultSchema<MfaEmailResponseSchema>.Failure("User profile reference not found.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var securityCode = RandomNumberGenerator.GetInt32(100000, 1_000_000)
            .ToString(CultureInfo.InvariantCulture);

        var mfaConfig = await context.MfaConfigurations
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (mfaConfig is null)
        {
            mfaConfig = new MfaConfiguration
            {
                CreatedAt = now,
                Id = Guid.NewGuid(),
                IsEnabled = true,
                Provider = _emailProvider,
                SecretKeyHash = ComputeCodeHash(securityCode),
                CodeExpiresAt = now.AddMinutes(15),
                ChallengeDeviceSessionId = deviceSessionId,
                UpdatedAt = now,
                UserId = userId
            };
            context.MfaConfigurations.Add(mfaConfig);
        }
        else
        {
            mfaConfig.IsEnabled = true;
            mfaConfig.Provider = _emailProvider;
            mfaConfig.SecretKeyHash = ComputeCodeHash(securityCode);
            mfaConfig.CodeExpiresAt = now.AddMinutes(15);
            mfaConfig.ChallengeDeviceSessionId = deviceSessionId;
            mfaConfig.UpdatedAt = now;
        }

        await emailService.SendMfaCodeEmailAsync(user.Email, user.Name, securityCode);
        await context.SaveChangesAsync(cancellationToken);

        var emailParts = user.Email.Split('@');
        var maskedEmail = emailParts[0].Length > 2
            ? $"{emailParts[0][..2]}***@{emailParts[1]}"
            : $"***@{emailParts[1]}";

        var response = new MfaEmailResponseSchema(maskedEmail, true, userId);
        return ResultSchema<MfaEmailResponseSchema>.Success(response);
    }

    public async Task<ResultSchema<bool>> VerifyMfaAsync(MfaVerificationRequestSchema request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mfaConfig = await context.MfaConfigurations
            .FirstOrDefaultAsync(m => m.UserId == request.UserId, cancellationToken);

        if (mfaConfig is null || !mfaConfig.IsEnabled || mfaConfig.Provider != _emailProvider)
        {
            return ResultSchema<bool>.Failure("MFA via email security policy is not enabled for this profile.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ResultSchema<bool>.Failure("Verification digits code parameter cannot be empty.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (mfaConfig.CodeExpiresAt is null || now >= mfaConfig.CodeExpiresAt.Value)
        {
            ClearChallenge(mfaConfig);
            await context.SaveChangesAsync(cancellationToken);
            return ResultSchema<bool>.Failure("Security token verification code has expired.");
        }

        if (mfaConfig.ChallengeDeviceSessionId != request.DeviceSessionId)
        {
            return ResultSchema<bool>.Failure("Security token verification code mismatch.");
        }

        var hashedInputCode = ComputeCodeHash(request.Code);
        if (!HashesEqual(mfaConfig.SecretKeyHash, hashedInputCode))
        {
            return ResultSchema<bool>.Failure("Security token verification code mismatch.");
        }

        if (context.Database.IsRelational())
        {
            var affectedRows = await context.MfaConfigurations
                .Where(configuration =>
                    configuration.Id == mfaConfig.Id &&
                    configuration.IsEnabled &&
                    configuration.Provider == _emailProvider &&
                    configuration.SecretKeyHash == mfaConfig.SecretKeyHash &&
                    configuration.CodeExpiresAt > now &&
                    configuration.ChallengeDeviceSessionId == request.DeviceSessionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(configuration => configuration.SecretKeyHash, string.Empty)
                    .SetProperty(configuration => configuration.CodeExpiresAt, (DateTime?)null)
                    .SetProperty(configuration => configuration.ChallengeDeviceSessionId, (Guid?)null),
                    cancellationToken);

            if (affectedRows != 1)
            {
                return ResultSchema<bool>.Failure("Security token verification code mismatch.");
            }

            ClearChallenge(mfaConfig);
            context.Entry(mfaConfig).State = EntityState.Unchanged;
        }
        else
        {
            ClearChallenge(mfaConfig);
            await context.SaveChangesAsync(cancellationToken);
        }

        return ResultSchema<bool>.Success(true);
    }

    private static void ClearChallenge(MfaConfiguration configuration)
    {
        configuration.SecretKeyHash = string.Empty;
        configuration.CodeExpiresAt = null;
        configuration.ChallengeDeviceSessionId = null;
    }

    private string ComputeCodeHash(string rawCode)
    {
        var pepper = configuration["MFA_CODE_PEPPER"] ?? configuration["JWT_SECRET_KEY"];
        if (string.IsNullOrWhiteSpace(pepper))
        {
            throw new InvalidOperationException("MFA code pepper is not configured.");
        }

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes(rawCode));
        return Convert.ToHexString(hash);
    }

    private static bool HashesEqual(string expectedHash, string actualHash)
    {
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHash),
            Convert.FromHexString(actualHash));
    }
}
