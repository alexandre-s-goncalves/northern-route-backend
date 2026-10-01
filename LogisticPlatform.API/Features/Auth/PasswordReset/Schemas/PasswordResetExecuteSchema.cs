namespace LogisticPlatform.API.Features.Auth.PasswordReset.Schemas;

public sealed record PasswordResetExecuteSchema(
    string NewPassword,
    string Token
);
