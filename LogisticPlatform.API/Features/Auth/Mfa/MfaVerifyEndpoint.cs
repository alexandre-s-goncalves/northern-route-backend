using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using LogisticPlatform.API.Features.Auth.Mfa.Schemas;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Features.Auth.Mfa;

public sealed class MfaVerifyModule : IModule
{
    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/mfa/verify", async (
            MfaVerificationRequestSchema request,
            AppDbContext context,
            IMfaService mfaService,
            IRefreshTokenService refreshTokenService,
            ITokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            var mfaResult = await mfaService.VerifyMfaAsync(request, cancellationToken);

            if (!mfaResult.IsSuccess)
            {
                return Results.BadRequest(mfaResult);
            }

            var user = await context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user is null)
            {
                return Results.NotFound("User profile reference associated with verification not found.");
            }

            var deviceSession = await context.UserDeviceSessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.IsActive, cancellationToken);

            var deviceSessionId = deviceSession?.Id ?? Guid.NewGuid();

            var accessToken = tokenService.GenerateToken(user);
            var refreshToken = await refreshTokenService.CreateTokenAsync(user.Id, deviceSessionId, cancellationToken);

            var response = new LoginResponseSchema(
                user.Id,
                user.Name,
                user.Email,
                user.Role?.Name ?? "USER",
                accessToken,
                refreshToken,
                deviceSessionId,
                false
            );

            return Results.Ok(response);
        })
        .WithName("Auth_MfaVerify")
        .WithTags("Authentication");

        return endpoints;
    }
}
