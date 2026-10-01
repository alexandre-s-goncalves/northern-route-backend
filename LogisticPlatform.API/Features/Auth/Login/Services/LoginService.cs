using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
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
    IPasswordHashService passwordHashService,
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
        var normalizedEmail = request.Email.ToUpperInvariant();
        var legacyNormalizedEmail = new string([.. request.Email.Select(char.ToLowerInvariant)]);
        var user = await users.FirstOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail || candidate.Email == legacyNormalizedEmail,
            cancellationToken);

        if (user is null)
        {
            var ghostAudit = new LoginAudit(null, null, ipAddress, userAgent, "FAILED");
            context.LoginAudits.Add(ghostAudit);
            await context.SaveChangesAsync(cancellationToken);

            return ResultSchema<LoginResponseSchema>.Failure("Invalid credentials.");
        }

        if (!passwordHashService.VerifyPassword(user, request.Password))
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

        var mfaConfiguration = await context.MfaConfigurations
            .FirstOrDefaultAsync(configuration =>
                configuration.UserId == user.Id &&
                configuration.IsEnabled &&
                configuration.Provider == "email",
                cancellationToken);
        var isMfaRequired = mfaConfiguration is not null;
        var auditStatus = isMfaRequired ? "MFA_PENDING" : "SUCCESS";
        var successAudit = new LoginAudit(user.Id, deviceSessionId, ipAddress, userAgent, auditStatus);
        context.LoginAudits.Add(successAudit);

        if (isMfaRequired)
        {
            await mfaService.SendEmailCodeAsync(user.Id, deviceSessionId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var mfaResponse = new LoginResponseSchema(
                user.Id,
                user.Name,
                user.Email,
                user.Role?.Name ?? "USER",
                string.Empty,
                string.Empty,
                deviceSessionId ?? Guid.Empty,
                true);

            return ResultSchema<LoginResponseSchema>.Success(mfaResponse);
        }

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
