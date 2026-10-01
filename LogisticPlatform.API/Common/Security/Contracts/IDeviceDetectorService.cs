using System;
using LogisticPlatform.API.Common.Domain;
using Microsoft.AspNetCore.Http;

namespace LogisticPlatform.API.Common.Security.Contracts;

public interface IDeviceDetectorService
{
    UserDeviceSession ResolveDeviceDetails(HttpContext httpContext, Guid userId);
}
