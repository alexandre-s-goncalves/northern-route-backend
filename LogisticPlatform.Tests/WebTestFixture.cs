using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LogisticPlatform.Tests;

public class WebTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                { "JWT_SECRET_KEY", "SuperSecretSecureKeyForNorthernRouteLogisticsTests2026" }
            };

            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<TestEmailService>();
            services.AddSingleton<IEmailService>(serviceProvider =>
                serviceProvider.GetRequiredService<TestEmailService>());

            var descriptorsToRemove = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    descriptor.ServiceType == typeof(DbContextOptions) ||
                    descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>) ||
                    descriptor.ServiceType == typeof(AppDbContext))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("LogisticPlatform_Unified_Suite_InMemory_DB");
            });
        });
    }
}

public sealed class TestEmailService : IEmailService
{
    private readonly ConcurrentDictionary<string, (string UserName, string SecurityCode)> _sentMessages = new();

    public Task SendMfaCodeEmailAsync(string toEmail, string userName, string securityCode)
    {
        _sentMessages[toEmail] = (userName, securityCode);
        return Task.CompletedTask;
    }

    public (string UserName, string SecurityCode) GetLastMessage(string toEmail)
    {
        return _sentMessages[toEmail];
    }
}
