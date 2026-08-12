namespace LogisticPlatform.API.Features.Auth.Login.Schemas;

internal sealed record LoginRequestSchema(string Email, string Password);
internal sealed record LoginResponseSchema(Guid UserId, string Name, string Email, string Role, string Token);
