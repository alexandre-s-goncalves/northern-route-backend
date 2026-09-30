using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Features.Auth.Login.Services;

internal sealed class LoginService(
    AppDbContext context,
    IDeviceDetectorService deviceDetectorService,
    IHttpContextAccessor httpContextAccessor,
    IMfaService mfaService,
    IRefreshTokenService refreshTokenService,
    ITokenService tokenService) : ILoginService
{
    public async Task<ResultSchema<LoginResponseSchema>> ExecuteAsync(
        LoginRequestSchema request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "UNKNOWN";
        var userAgent = httpContext?.Request?.Headers.UserAgent.ToString() ?? "UNKNOWN";

        var users = context.Users.Include(u => u.Role);
        User? user;

        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            user = null;
            await foreach (var candidate in users.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                if (string.Equals(candidate.Email, request.Email, StringComparison.OrdinalIgnoreCase))
                {
                    user = candidate;
                    break;
                }
            }
        }
        else
        {
            user = await users.FirstOrDefaultAsync(
                u => EF.Functions.ILike(u.Email, request.Email),
                cancellationToken);
        }

        if (user is null)
        {
            var ghostAudit = new LoginAudit(Guid.Empty, null, ipAddress, userAgent, "FAILED");
            context.LoginAudits.Add(ghostAudit);
            await context.SaveChangesAsync(cancellationToken);

            return ResultSchema<LoginResponseSchema>.Failure("Invalid credentials.");
        }

        if (user.PasswordHash != request.Password)
        {
            var failedAudit = new LoginAudit(user.Id, null, ipAddress, userAgent, "FAILED");
            context.LoginAudits.Add(failedAudit);
            await context.SaveChangesAsync(cancellationToken);

            return ResultSchema<LoginResponseSchema>.Failure("Invalid credentials.");
        }

        Guid? deviceSessionId = null;

        if (httpContext is not null)
        {
            var deviceSession = deviceDetectorService.ResolveDeviceDetails(httpContext, user.Id);
            context.UserDeviceSessions.Add(deviceSession);
            deviceSessionId = deviceSession.Id;
        }

        var mfaConfig = await context.MfaConfigurations
            .FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);

        if (mfaConfig is not null && mfaConfig.IsEnabled && mfaConfig.Provider == "email")
        {
            var mfaAudit = new LoginAudit(user.Id, deviceSessionId, ipAddress, userAgent, "MFA_PENDING");
            context.LoginAudits.Add(mfaAudit);
            await context.SaveChangesAsync(cancellationToken);

            await mfaService.SendEmailCodeAsync(user.Id, cancellationToken);

            var mfaResponse = new LoginResponseSchema(
                user.Id,
                user.Name,
                user.Email,
                user.Role?.Name ?? "USER",
                string.Empty,
                string.Empty,
                deviceSessionId ?? Guid.Empty,
                true
            );

            return ResultSchema<LoginResponseSchema>.Success(mfaResponse);
        }

        var successAudit = new LoginAudit(user.Id, deviceSessionId, ipAddress, userAgent, "SUCCESS");
        context.LoginAudits.Add(successAudit);
        await context.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.GenerateToken(user);
        var refreshToken = string.Empty;

        if (deviceSessionId.HasValue)
        {
            refreshToken = await refreshTokenService.CreateTokenAsync(user.Id, deviceSessionId.Value, cancellationToken);
        }

        var response = new LoginResponseSchema(
            user.Id,
            user.Name,
            user.Email,
            user.Role?.Name ?? "USER",
            accessToken,
            refreshToken,
            deviceSessionId ?? Guid.Empty,
            false
        );

        return ResultSchema<LoginResponseSchema>.Success(response);
    }
}
