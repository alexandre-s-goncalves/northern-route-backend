using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LogisticPlatform.API.Common.Data;
using Xunit;

namespace LogisticPlatform.Tests;

public sealed class DatabaseTestFixture : IAsyncLifetime
{
    internal AppDbContext Context { get; private set; } = null!;
    private string _testDatabaseName = null!;
    private string _masterConnectionString = null!;

    public async Task InitializeAsync()
    {
        _masterConnectionString = "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=YourSecurePassword2026;";

        _testDatabaseName = $"logistic_platform_test_{Guid.NewGuid().ToString("N")[..8]}";
        var testConnectionString = $"Host=127.0.0.1;Port=5432;Database={_testDatabaseName};Username=postgres;Password=YourSecurePassword2026;";

        var masterOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_masterConnectionString)
            .Options;

        using (var masterContext = new AppDbContext(masterOptions))
        {
            await masterContext.Database.ExecuteSqlRawAsync("CREATE DATABASE " + _testDatabaseName + ";");
        }

        var testOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(testConnectionString)
            .Options;

        Context = new AppDbContext(testOptions);
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Context != null)
        {
            await Context.DisposeAsync();
        }

        var masterOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_masterConnectionString)
            .Options;

        using var masterContext = new AppDbContext(masterOptions);

        var dropCommand = "REVOKE CONNECT ON DATABASE " + _testDatabaseName + " FROM public; " +
                          "SELECT pg_terminate_backend(pg_stat_activity.pid) FROM pg_stat_activity " +
                          "WHERE pg_stat_activity.datname = '" + _testDatabaseName + "' AND pid <> pg_backend_pid(); " +
                          "DROP DATABASE IF EXISTS " + _testDatabaseName + ";";

        await masterContext.Database.ExecuteSqlRawAsync(dropCommand);
    }
}
