using LogisticPlatform.API.Common;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Schemas;

namespace LogisticPlatform.API.Features.Auth.Login;

internal sealed class LoginEndpoint : IModule
{
    public LoginEndpoint()
    {
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/auth/login", async (
            LoginRequestSchema request,
            ILoginService loginService,
            CancellationToken cancellationToken) =>
        {
            var result = await loginService.ExecuteAsync(request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        })
        .WithName("Auth_Login")
        .WithSummary("Authenticates enterprise users and drivers")
        .WithDescription("Validates credentials and issues an authentication token.")
        .Produces<ResultSchema<LoginResponseSchema>>(StatusCodes.Status200OK)
        .Produces<ResultSchema<LoginResponseSchema>>(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
