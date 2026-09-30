using System;

namespace LogisticPlatform.API.Features.Auth.Mfa.Schemas;

public sealed record MfaVerificationRequestSchema(
    string Code,
    Guid UserId
);
