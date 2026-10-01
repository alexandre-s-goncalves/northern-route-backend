using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Features.Auth.PasswordReset.Schemas;

namespace LogisticPlatform.API.Features.Auth.PasswordReset.Services;

public interface IPasswordResetServiceFeature
{
    Task<ResultSchema<bool>> RequestResetAsync(PasswordResetRequestSchema request, CancellationToken cancellationToken);
    Task<ResultSchema<bool>> ResetPasswordAsync(PasswordResetExecuteSchema request, CancellationToken cancellationToken);
}
