using Microsoft.EntityFrameworkCore;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Schemas;

namespace LogisticPlatform.API.Features.Auth.Login.Services;

internal sealed class LoginService(AppDbContext context, ITokenService tokenService) : ILoginService
{
    public async Task<ResultSchema<LoginResponseSchema>> ExecuteAsync(
        LoginRequestSchema request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u =>
                string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

        if (user is null)
        {
            return ResultSchema<LoginResponseSchema>.Failure("Invalid credentials.");
        }

        if (user.PasswordHash != request.Password)
        {
            return ResultSchema<LoginResponseSchema>.Failure("Invalid credentials.");
        }

        var generatedToken = tokenService.GenerateToken(user);

        var response = new LoginResponseSchema(
            user.Id,
            user.Name,
            user.Email,
            user.Role?.Name ?? "USER",
            generatedToken
        );

        return ResultSchema<LoginResponseSchema>.Success(response);
    }
}
