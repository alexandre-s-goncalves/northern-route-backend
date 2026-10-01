using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using LogisticPlatform.API.Features.Auth.Refresh.Schemas;
using LogisticPlatform.API.Features.Auth.Refresh.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LogisticPlatform.Tests.Common;

public sealed class RelationalSecurityPersistenceTests
{
    [Fact]
    public async Task SaveChanges_ShouldSoftDeleteSynchronously_AndFilterDeletedMfa()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var user = await AddUserAsync(context);
        var configuration = new MfaConfiguration
        {
            Id = Guid.NewGuid(),
            IsEnabled = true,
            Provider = "email",
            SecretKeyHash = "challenge",
            CodeExpiresAt = DateTime.UtcNow.AddMinutes(15),
            UserId = user.Id
        };
        context.MfaConfigurations.Add(configuration);
        await context.SaveChangesAsync();

        context.MfaConfigurations.Remove(configuration);
        await context.SaveChangesAsync();

        Assert.True(configuration.IsDeleted);
        Assert.NotNull(configuration.DeletedAt);
        Assert.Empty(await context.MfaConfigurations.ToListAsync());
        Assert.Single(await context.MfaConfigurations.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSetUpdatedAt_WhenEntityIsModified()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var user = await AddUserAsync(context);
        var session = new UserDeviceSession
        {
            BrowserName = "Test",
            BrowserVersion = "1",
            DeviceModel = "Test Device",
            DeviceType = "desktop",
            Id = Guid.NewGuid(),
            IpAddress = "127.0.0.1",
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            LocationCity = "UNKNOWN",
            LocationCountry = "UNKNOWN",
            OsName = "Test OS",
            OsVersion = "1",
            UserId = user.Id
        };
        context.UserDeviceSessions.Add(session);
        await context.SaveChangesAsync();

        session.IsActive = false;
        await context.SaveChangesAsync();

        Assert.NotNull(session.UpdatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldKeepAuditAndNullUserId_WhenUserIsHardDeleted()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var user = await AddUserAsync(context);
        var audit = new LoginAudit(user.Id, null, "127.0.0.1", "test-agent", "SUCCESS");
        context.LoginAudits.Add(audit);
        await context.SaveChangesAsync();

        context.Users.Remove(user);
        await context.SaveChangesAsync();

        var retainedAudit = await context.LoginAudits.SingleAsync();
        Assert.Null(retainedAudit.UserId);
        Assert.Equal("SUCCESS", retainedAudit.Status);
    }

    [Fact]
    public async Task RefreshService_ShouldAtomicallyConsumeOldToken_AndCreateReplacement()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var user = await AddUserAsync(context);
        var deviceSession = new UserDeviceSession
        {
            BrowserName = "Test",
            BrowserVersion = "1",
            DeviceModel = "Test Device",
            DeviceType = "desktop",
            Id = Guid.NewGuid(),
            IpAddress = "127.0.0.1",
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            LocationCity = "UNKNOWN",
            LocationCountry = "UNKNOWN",
            OsName = "Test OS",
            OsVersion = "1",
            UserId = user.Id
        };
        context.UserDeviceSessions.Add(deviceSession);
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("JWT_SECRET_KEY", "SecurityIntegrationTestSigningKey2026_32Chars")
            ])
            .Build();
        var refreshTokens = new RefreshTokenService(context, TimeProvider.System);
        var refreshService = new RefreshService(context, refreshTokens, new TokenService(configuration));
        var oldToken = await refreshTokens.CreateTokenAsync(user.Id, deviceSession.Id, CancellationToken.None);

        var firstRotation = await refreshService.ExecuteAsync(new RefreshRequestSchema(oldToken), CancellationToken.None);
        var replay = await refreshService.ExecuteAsync(new RefreshRequestSchema(oldToken), CancellationToken.None);

        Assert.True(firstRotation.IsSuccess);
        Assert.NotNull(firstRotation.Data);
        Assert.NotEqual(oldToken, firstRotation.Data.RefreshToken);
        Assert.False(replay.IsSuccess);
        var sessions = await context.RefreshTokenSessions.AsNoTracking().ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, session => session.IsRevoked);
        Assert.Single(sessions, session => !session.IsRevoked);
    }

    private static DbContextOptions<AppDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return new AppDbContext(CreateOptions(connection));
    }

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<User> AddUserAsync(AppDbContext context)
    {
        var role = new Role($"ROLE_{Guid.NewGuid():N}");
        var user = new User("Relational Security Test", $"relational-{Guid.NewGuid():N}@example.com", "unused", role.Id);
        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
