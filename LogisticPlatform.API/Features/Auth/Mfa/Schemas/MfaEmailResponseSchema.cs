using System;

namespace LogisticPlatform.API.Features.Auth.Mfa.Schemas;

public sealed record MfaEmailResponseSchema(
    string MaskedEmail,
    bool MessageDispatched,
    Guid UserId
);
