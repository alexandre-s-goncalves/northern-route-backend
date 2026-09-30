using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Features.Auth.Refresh.Schemas;

namespace LogisticPlatform.API.Features.Auth.Refresh.Services;

public interface IRefreshService
{
    Task<ResultSchema<RefreshResponseSchema>> ExecuteAsync(RefreshRequestSchema request, CancellationToken cancellationToken);
}
