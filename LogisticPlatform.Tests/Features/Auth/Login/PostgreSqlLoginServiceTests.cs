using System.Net;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using LogisticPlatform.API.Features.Auth.Login.Services;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.Login;

public sealed class PostgreSqlLoginServiceTests
{
    [PostgreSqlFact]
    public async Task ExecuteAsync_ShouldAuthenticateCaseInsensitiveEmail_WhenUsingPostgreSqlProvider()
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION_STRING")!;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var role = new Role($"TEST_{Guid.NewGuid():N}");
        var email = $"login-a{Guid.NewGuid():N}@test.com";
        var user = new User("PostgreSQL Test", email, "SecurePassword789", role.Id);
        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET_KEY"] = "SuperSecretSecureKeyForNorthernRouteLogistics2026"
            })
            .Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        httpContext.Request.Headers.UserAgent = "PostgreSQL-Integration-Test";
        var loginService = new LoginService(
            context,
            new DeviceDetectorService(),
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeMfaService(),
            new RefreshTokenService(context),
            new TokenService(configuration));

        var result = await loginService.ExecuteAsync(
            new LoginRequestSchema(email, "SecurePassword789"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(user.Id, result.Data.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION_STRING")))
        {
            Skip = "Set POSTGRES_TEST_CONNECTION_STRING to a dedicated PostgreSQL test database.";
        }
    }
}
