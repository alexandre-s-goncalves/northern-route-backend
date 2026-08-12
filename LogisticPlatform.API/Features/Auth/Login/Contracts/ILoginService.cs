using LogisticPlatform.API.Common;
using LogisticPlatform.API.Features.Auth.Login.Schemas;

namespace LogisticPlatform.API.Features.Auth.Login.Contracts;

internal interface ILoginService
{
    Task<ResultSchema<LoginResponseSchema>> ExecuteAsync(LoginRequestSchema request, CancellationToken cancellationToken);
}
