using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Refresh.Schemas;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Features.Auth.Refresh.Services;

internal sealed class RefreshService(
    AppDbContext context,
    IRefreshTokenService refreshTokenService,
    ITokenService tokenService) : IRefreshService
{
    public async Task<ResultSchema<RefreshResponseSchema>> ExecuteAsync(
        RefreshRequestSchema request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var revokedSession = await refreshTokenService.ValidateAndRotateTokenAsync(request.RefreshToken, cancellationToken);

        if (revokedSession is null)
        {
            return ResultSchema<RefreshResponseSchema>.Failure("Invalid, expired, or revoked refresh token.");
        }

        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == revokedSession.UserId, cancellationToken);

        if (user is null)
        {
            return ResultSchema<RefreshResponseSchema>.Failure("User reference associated with token not found.");
        }

        var newAccessToken = tokenService.GenerateToken(user);
        var newRefreshToken = await refreshTokenService.CreateTokenAsync(user.Id, revokedSession.DeviceSessionId, cancellationToken);

        var response = new RefreshResponseSchema(newAccessToken, newRefreshToken);
        return ResultSchema<RefreshResponseSchema>.Success(response);
    }
}
