namespace LogisticPlatform.API.Common;

internal interface IModule
{
    IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints);
}
