using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Features.Auth.Login.Services;

internal sealed class LoginService(
    AppDbContext context,
    IHttpContextAccessor httpContextAccessor,
    ITokenService tokenService) : ILoginService
{
    public async Task<ResultSchema<LoginResponseSchema>> ExecuteAsync(
        LoginRequestSchema request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "UNKNOWN";
        var userAgent = httpContext?.Request?.Headers.UserAgent.ToString() ?? "UNKNOWN";

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
            var ghostAudit = new LoginAudit(Guid.Empty, ipAddress, userAgent, "FAILED");
            context.LoginAudits.Add(ghostAudit);
            await context.SaveChangesAsync(cancellationToken);

            return ResultSchema<LoginResponseSchema>.Failure("Invalid credentials.");
        }

        if (user.PasswordHash != request.Password)
        {
            var failedAudit = new LoginAudit(user.Id, ipAddress, userAgent, "FAILED");
            context.LoginAudits.Add(failedAudit);
            await context.SaveChangesAsync(cancellationToken);

            return ResultSchema<LoginResponseSchema>.Failure("Invalid credentials.");
        }

        var successAudit = new LoginAudit(user.Id, ipAddress, userAgent, "SUCCESS");
        context.LoginAudits.Add(successAudit);
        await context.SaveChangesAsync(cancellationToken);

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
