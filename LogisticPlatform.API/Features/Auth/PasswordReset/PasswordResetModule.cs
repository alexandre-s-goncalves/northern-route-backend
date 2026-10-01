using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Features.Auth.PasswordReset.Schemas;
using LogisticPlatform.API.Features.Auth.PasswordReset.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LogisticPlatform.API.Features.Auth.PasswordReset;

public sealed class PasswordResetModule : IModule
{
    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/auth/password-reset/request", async (
            PasswordResetRequestSchema request,
            IPasswordResetServiceFeature featureService,
            CancellationToken cancellationToken) =>
        {
            var result = await featureService.RequestResetAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("Auth_PasswordResetRequest")
        .WithTags("Authentication");

        endpoints.MapPost("/api/auth/password-reset/reset", async (
            PasswordResetExecuteSchema request,
            IPasswordResetServiceFeature featureService,
            CancellationToken cancellationToken) =>
        {
            var result = await featureService.ResetPasswordAsync(request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("Auth_PasswordResetExecute")
        .WithTags("Authentication");

        return endpoints;
    }
}
