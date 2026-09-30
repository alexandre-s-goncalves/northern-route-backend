using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Features.Auth.Refresh.Schemas;
using LogisticPlatform.API.Features.Auth.Refresh.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LogisticPlatform.API.Features.Auth.Refresh;

public sealed class RefreshModule : IModule
{
    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/refresh", async (
            RefreshRequestSchema request,
            IRefreshService refreshService,
            CancellationToken cancellationToken) =>
        {
            var result = await refreshService.ExecuteAsync(request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("Auth_RefreshToken")
        .WithTags("Authentication");

        return endpoints;
    }
}
