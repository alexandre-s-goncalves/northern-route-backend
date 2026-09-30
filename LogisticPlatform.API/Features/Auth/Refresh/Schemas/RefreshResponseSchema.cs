using System;

namespace LogisticPlatform.API.Features.Auth.Refresh.Schemas;

public sealed record RefreshResponseSchema(
    string AccessToken,
    string RefreshToken
);
