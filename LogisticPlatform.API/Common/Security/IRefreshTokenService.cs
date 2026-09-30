using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Domain;

namespace LogisticPlatform.API.Common.Security;

public interface IRefreshTokenService
{
    Task<string> CreateTokenAsync(Guid userId, Guid deviceSessionId, CancellationToken cancellationToken);
    Task<RefreshTokenSession?> ValidateAndRotateTokenAsync(string token, CancellationToken cancellationToken);
}
