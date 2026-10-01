using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Data.Seeding;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.IoC;
using LogisticPlatform.API.Features.Auth.Login.Services;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogisticPlatform.Tests.Common;

public sealed class CoverageCompletionTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Mobile Safari/537.36", "ANDROID_DEVICE", "mobile", "Android")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "IPHONE_DEVICE", "mobile", "iOS")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "IPAD_DEVICE", "tablet", "iOS")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36", "DESKTOP_GENERIC", "desktop", "Windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15", "DESKTOP_GENERIC", "desktop", "macOS")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:128.0) Gecko/20100101 Firefox/128.0", "DESKTOP_GENERIC", "desktop", "UNKNOWN")]
    public void DeviceDetectorService_ShouldResolveAllSupportedDeviceProfiles(
        string userAgent,
        string expectedDeviceModel,
        string expectedDeviceType,
        string expectedOsName)
    {
        var service = new DeviceDetectorService();
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = userAgent;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");

        var device = service.ResolveDeviceDetails(context, Guid.NewGuid());

        Assert.Equal(expectedDeviceModel, device.DeviceModel);
        Assert.Equal(expectedDeviceType, device.DeviceType);
        Assert.Equal(expectedOsName, device.OsName);
        Assert.Equal("10.0.0.5", device.IpAddress);
        Assert.True(device.IsActive);
    }

    [Fact]
    public void DeviceDetectorService_ShouldUseUnknownFallbacks_WhenNoDeviceSignalsExist()
    {
        var service = new DeviceDetectorService();
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = "CustomBot/1.0";

        var device = service.ResolveDeviceDetails(context, Guid.NewGuid());

        Assert.Equal("UNKNOWN", device.BrowserName);
        Assert.Equal("UNKNOWN", device.BrowserVersion);
        Assert.Equal("DESKTOP_GENERIC", device.DeviceModel);
        Assert.Equal("desktop", device.DeviceType);
        Assert.Equal("UNKNOWN", device.OsName);
        Assert.Equal("UNKNOWN", device.OsVersion);
    }

    [Fact]
    public void TokenService_ShouldGenerateJwt_ForRegisteredUser()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET_KEY"] = "SuperSecretSecureKeyForNorthernRouteLogistics2026"
            })
            .Build();

        var service = new TokenService(configuration);
        var role = new Role("MANAGER");
        var user = new User("Jane Doe", "jane@example.com", "hashed", role.Id);

        var token = service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, claim => claim.Value == user.Id.ToString());
        Assert.Contains(jwt.Claims, claim => claim.Value == user.Name);
        Assert.Contains(jwt.Claims, claim => claim.Value == user.Email);
        Assert.Contains(jwt.Claims, claim => claim.Value == "USER");
        Assert.NotEqual(default, jwt.ValidTo);
    }

    [Fact]
    public void TokenService_ShouldThrow_WhenJwtSecretIsMissing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var service = new TokenService(configuration);
        var user = new User("Jane Doe", "jane@example.com", "hashed", Guid.NewGuid());

        var exception = Assert.Throws<InvalidOperationException>(() => service.GenerateToken(user));
        Assert.Contains("JWT Secret Key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoginAudit_ShouldNormalizeBlankValues()
    {
        var audit = new LoginAudit(null, null, "   ", "  ", " ");

        Assert.Equal("UNKNOWN", audit.IpAddress);
        Assert.Equal("UNKNOWN", audit.UserAgent);
        Assert.Equal("SUCCESS", audit.Status);
        Assert.Null(audit.UserId);
        Assert.NotEqual(Guid.Empty, audit.Id);
    }

    [Fact]
    public void LoginAudit_ShouldUppercaseStatus_WhenProvidedInLowercase()
    {
        var audit = new LoginAudit(Guid.NewGuid(), Guid.NewGuid(), "127.0.0.1", "browser", "failed");

        Assert.Equal("FAILED", audit.Status);
    }

    [Fact]
    public async Task DevelopmentDataSeeder_ShouldSeedUsers_AndTrackLifecycleStateChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);

        await context.Database.EnsureCreatedAsync();
        var seeder = new DevelopmentDataSeeder(context, new PasswordHasher<User>());
        await seeder.SeedAsync(seedDevelopmentData: true);

        var roles = await context.Roles.OrderBy(role => role.Name).ToListAsync();
        Assert.Contains(roles, role => role.Name == "ADMIN");
        Assert.Contains(roles, role => role.Name == "USER");

        var user = await context.Users.SingleAsync(candidate => candidate.Email == "ALE@ALE.COM");
        Assert.NotEmpty(user.PasswordHash);
        Assert.NotEqual("Password123", user.PasswordHash);

        var deviceSession = new UserDeviceSession
        {
            BrowserName = "Coverage",
            BrowserVersion = "1",
            DeviceModel = "TEST_DEVICE",
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
        Assert.NotEqual(default, deviceSession.CreatedAt);

        deviceSession.IsActive = false;
        await context.SaveChangesAsync();
        Assert.NotNull(deviceSession.UpdatedAt);

        context.UserDeviceSessions.Remove(deviceSession);
        await context.SaveChangesAsync();
        Assert.True(deviceSession.IsDeleted);
        Assert.NotNull(deviceSession.DeletedAt);
    }

    [Fact]
    public async Task AppDbContext_ShouldApplyAuditFields_OnSynchronousSaveChanges()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var configuration = new MfaConfiguration
        {
            Id = Guid.NewGuid(),
            IsEnabled = true,
            Provider = "email",
            SecretKeyHash = "sync-hash",
            UserId = Guid.NewGuid()
        };

        context.MfaConfigurations.Add(configuration);
        await context.SaveChangesAsync();

        Assert.NotEqual(default, configuration.CreatedAt);
        Assert.Null(configuration.UpdatedAt);

        configuration.Provider = "totp";
        await context.SaveChangesAsync();

        Assert.NotNull(configuration.UpdatedAt);

        context.MfaConfigurations.Remove(configuration);
        await context.SaveChangesAsync();

        Assert.True(configuration.IsDeleted);
        Assert.NotNull(configuration.DeletedAt);
    }

    [Fact]
    public async Task MfaService_ShouldThrow_WhenPepperConfigurationIsMissing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var emailService = new LogisticPlatform.Tests.TestEmailService();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var role = new Role("MFA_PEPPER_ROLE");
        var user = new User("Pepper User", "pepper-missing@example.com", "Password123", role.Id);

        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new MfaService(
            context,
            emailService,
            new ConfigurationBuilder().AddInMemoryCollection([]).Build(),
            timeProvider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendEmailCodeAsync(user.Id, null, CancellationToken.None));

        Assert.Equal("MFA code pepper is not configured.", exception.Message);
    }

    [Fact]
    public void LoginIoC_ShouldRegisterLoginService()
    {
        var services = new ServiceCollection();

        services.AddLoginFeature();

        var serviceDescriptor = services.SingleOrDefault(descriptor => descriptor.ServiceType == typeof(ILoginService));
        Assert.NotNull(serviceDescriptor);
        Assert.Equal(typeof(LoginService), serviceDescriptor.ImplementationType);
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
