using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Services;

namespace LogisticPlatform.Tests;

public sealed class WebTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("UseInMemoryTestDatabase", "true");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                { "JWT_SECRET_KEY", "SuperSecretSecureKeyForNorthernRouteLogistics2026" }
            };

            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("e2e_endpoint_test_db");
            });

            services.AddSingleton<ITokenService, TokenService>();
            services.AddScoped<ILoginService, LoginService>();
        });

        base.ConfigureWebHost(builder);
    }
}
