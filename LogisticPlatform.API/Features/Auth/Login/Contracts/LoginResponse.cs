using System;

namespace LogisticPlatform.API.Features.Auth.Login.Contracts;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public Guid DeviceSessionId { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
}
