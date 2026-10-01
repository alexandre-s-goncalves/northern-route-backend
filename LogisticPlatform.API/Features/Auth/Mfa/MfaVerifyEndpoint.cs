using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
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
            HttpContext httpContext,
            AppDbContext context,
            IMfaService mfaService,
            IRefreshTokenService refreshTokenService,
            ITokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            if (request.DeviceSessionId is null)
            {
                return Results.BadRequest("Device session is required to verify MFA.");
            }

            var user = await context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user is null)
            {
                return Results.NotFound("User profile reference associated with verification not found.");
            }

            var deviceSession = await context.UserDeviceSessions
                .FirstOrDefaultAsync(
                    session => session.Id == request.DeviceSessionId &&
                               session.UserId == user.Id &&
                               session.IsActive,
                    cancellationToken);

            if (deviceSession is null)
            {
                return Results.BadRequest("Device session associated with MFA challenge is invalid.");
            }

            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var mfaResult = await mfaService.VerifyMfaAsync(request, cancellationToken);
            if (!mfaResult.IsSuccess)
            {
                return Results.BadRequest(mfaResult);
            }

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "UNKNOWN";
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            context.LoginAudits.Add(new LoginAudit(user.Id, deviceSession.Id, ipAddress, userAgent, "SUCCESS"));

            var accessToken = tokenService.GenerateToken(user);
            var refreshToken = await refreshTokenService.CreateTokenAsync(user.Id, deviceSession.Id, cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            var response = new LoginResponseSchema(
                user.Id,
                user.Name,
                user.Email,
                user.Role?.Name ?? "USER",
                accessToken,
                refreshToken,
                deviceSession.Id,
                false
            );

            return Results.Ok(response);
        })
        .WithName("Auth_MfaVerify")
        .WithTags("Authentication");

        return endpoints;
    }
}
