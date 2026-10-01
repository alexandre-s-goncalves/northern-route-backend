using System;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security.Contracts;
using Microsoft.AspNetCore.Http;

namespace LogisticPlatform.API.Common.Security;

public sealed class DeviceDetectorService : IDeviceDetectorService
{
    private const string _unknown = "UNKNOWN";

    public UserDeviceSession ResolveDeviceDetails(HttpContext httpContext, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? _unknown;
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        var browserName = _unknown;
        var browserVersion = _unknown;
        var deviceModel = "DESKTOP_GENERIC";
        var deviceType = "desktop";
        var osName = _unknown;
        var osVersion = _unknown;

        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            deviceModel = "ANDROID_DEVICE";
            deviceType = "mobile";
            osName = "Android";
        }
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
        {
            deviceModel = "IPHONE_DEVICE";
            deviceType = "mobile";
            osName = "iOS";
        }
        else if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            deviceModel = "IPAD_DEVICE";
            deviceType = "tablet";
            osName = "iOS";
        }
        else if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            osName = "Windows";
        }
        else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase))
        {
            osName = "macOS";
        }

        if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            browserName = "Firefox";
        }
        else if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
        {
            browserName = "Chrome";
        }
        else if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
        {
            browserName = "Safari";
        }

        return new UserDeviceSession
        {
            BrowserName = browserName,
            BrowserVersion = browserVersion,
            CreatedAt = DateTime.UtcNow,
            DeviceModel = deviceModel,
            DeviceType = deviceType,
            Id = Guid.NewGuid(),
            IpAddress = ipAddress,
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            LocationCity = _unknown,
            LocationCountry = _unknown,
            OsName = osName,
            OsVersion = osVersion,
            UserId = userId
        };
    }
}
