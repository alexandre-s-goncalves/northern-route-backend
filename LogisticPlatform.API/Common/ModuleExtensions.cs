using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace LogisticPlatform.API.Common;

internal static class ModuleExtensions
{
    public static WebApplication RegisterModules(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var moduleTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

        foreach (var type in moduleTypes)
        {
            if (Activator.CreateInstance(type) is IModule module)
            {
                module.MapEndpoints(app);
            }
        }

        return app;
    }
}
