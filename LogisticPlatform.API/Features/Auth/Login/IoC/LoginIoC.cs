using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Services;

namespace LogisticPlatform.API.Features.Auth.Login.IoC;

internal static class LoginIoC
{
    public static IServiceCollection AddLoginFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ILoginService, LoginService>();
        return services;
    }
}
