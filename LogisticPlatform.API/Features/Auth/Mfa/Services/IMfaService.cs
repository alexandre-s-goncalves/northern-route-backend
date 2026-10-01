using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Features.Auth.Mfa.Schemas;

namespace LogisticPlatform.API.Features.Auth.Mfa.Services;

public interface IMfaService
{
    Task<ResultSchema<MfaEmailResponseSchema>> SendEmailCodeAsync(
        Guid userId,
        Guid? deviceSessionId,
        CancellationToken cancellationToken);
    Task<ResultSchema<bool>> VerifyMfaAsync(MfaVerificationRequestSchema request, CancellationToken cancellationToken);
}
