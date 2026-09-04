using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Features.Auth.Login.Services;

internal sealed class LoginService(AppDbContext context, ITokenService tokenService) : ILoginService
{
    public async Task<ResultSchema<LoginResponseSchema>> ExecuteAsync(
        LoginRequestSchema request,
        CancellationToken cancellationToken)
    {
        var users = context.Users.Include(u => u.Role);
        User? user;

        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            user = null;
            await foreach (var candidate in users.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                if (string.Equals(candidate.Email, request.Email, StringComparison.OrdinalIgnoreCase))
                {
                    user = candidate;
                    break;
                }
            }
        }
        else
        {
            user = await users.FirstOrDefaultAsync(
                u => EF.Functions.ILike(u.Email, request.Email),
                cancellationToken);
        }

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
