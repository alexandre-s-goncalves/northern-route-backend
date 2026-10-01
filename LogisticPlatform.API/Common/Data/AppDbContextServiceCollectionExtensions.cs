using System;
using LogisticPlatform.API.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LogisticPlatform.API.Common.Data;

internal static class AppDbContextServiceCollectionExtensions
{
    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddDbContext<AppDbContext>(options => ConfigureDbContext(options, configuration, environment));
        return services;
    }

    private static void ConfigureDbContext(
        DbContextOptionsBuilder options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            options.UseNpgsql("Host=localhost;Database=logistic_platform_test_stub;Username=postgres;Password=test");
        }
        else
        {
            var connectionString = configuration["DATABASE_URL"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = configuration.GetConnectionString("DefaultConnection");
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = "Host=localhost;Port=5432;Database=logistic_platform_dev;Username=postgres;Password=YourSecurePassword2026";
            }

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(5);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 2,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null);
            });
        }

        options.ConfigureWarnings(warnings => warnings.Ignore(
            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }
}
