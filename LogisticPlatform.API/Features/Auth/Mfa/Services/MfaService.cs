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
using LogisticPlatform.API.Features.Auth.Mfa.Schemas;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Features.Auth.Mfa.Services;

internal sealed class MfaService(AppDbContext context, IEmailService emailService) : IMfaService
{
    public async Task<ResultSchema<MfaEmailResponseSchema>> SendEmailCodeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ResultSchema<MfaEmailResponseSchema>.Failure("User profile reference not found.");
        }

        var codeBytes = new byte[4];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(codeBytes);
        var securityCode = (BitConverter.ToUInt32(codeBytes, 0) % 900000 + 100000)
            .ToString(CultureInfo.InvariantCulture);

        var mfaConfig = await context.MfaConfigurations
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (mfaConfig is null)
        {
            mfaConfig = new MfaConfiguration
            {
                CreatedAt = DateTime.UtcNow,
                Id = Guid.NewGuid(),
                IsEnabled = true,
                Provider = "email",
                SecretKeyHash = ComputeSha256Hash(securityCode),
                UpdatedAt = DateTime.UtcNow,
                UserId = userId
            };
            context.MfaConfigurations.Add(mfaConfig);
        }
        else
        {
            mfaConfig.IsEnabled = true;
            mfaConfig.Provider = "email";
            mfaConfig.SecretKeyHash = ComputeSha256Hash(securityCode);
            mfaConfig.UpdatedAt = DateTime.UtcNow;
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

        if (mfaConfig is null || !mfaConfig.IsEnabled || mfaConfig.Provider != "email")
        {
            return ResultSchema<bool>.Failure("MFA via email security policy is not enabled for this profile.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ResultSchema<bool>.Failure("Verification digits code parameter cannot be empty.");
        }

        var lastUpdatedAt = mfaConfig.UpdatedAt ?? mfaConfig.CreatedAt;
        var tokenLifetimeWindow = lastUpdatedAt.AddMinutes(15);
        if (DateTime.UtcNow > tokenLifetimeWindow)
        {
            mfaConfig.IsDeleted = true;
            mfaConfig.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return ResultSchema<bool>.Failure("Security token verification code has expired.");
        }

        var hashedInputCode = ComputeSha256Hash(request.Code);
        if (mfaConfig.SecretKeyHash != hashedInputCode)
        {
            return ResultSchema<bool>.Failure("Security token verification code mismatch.");
        }

        mfaConfig.IsDeleted = true;
        mfaConfig.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ResultSchema<bool>.Success(true);
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
