using System;

namespace LogisticPlatform.API.Features.Auth.Login.Schemas;

public sealed record LoginResponseSchema(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    string Token,
    Guid DeviceSessionId
);
