using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using LogisticPlatform.API.Features.Auth.Login.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using ApiResult = LogisticPlatform.API.Common.ResultSchema<LogisticPlatform.API.Features.Auth.Login.Schemas.LoginResponseSchema>;

namespace LogisticPlatform.Tests.Features.Auth.Login;

public sealed class LoginServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenService _tokenService;

    public LoginServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JWT_SECRET_KEY", "SuperSecretSecureKeyForNorthernRouteLogistics2026" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _tokenService = new TokenService(configuration);

        var defaultHttpContext = new DefaultHttpContext();
        defaultHttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        defaultHttpContext.Request.Headers.UserAgent = "Xunit-Integration-Test-Environment-Agent";

        _httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = defaultHttpContext
        };
    }

    private static DbContextOptions<AppDbContext> CreateNewInMemoryDatabaseOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact(DisplayName = "Auth - Login Service: Should return failure when user password does not match database hash")]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenPasswordIsIncorrect()
    {
        var options = CreateNewInMemoryDatabaseOptions();
        using var context = new AppDbContext(options);

        var testRole = new Role("DRIVER");
        var testUser = new User("Alexandre Test", "driver@test.com", "CorrectPassword123", testRole.Id);

        context.Roles.Add(testRole);
        context.Users.Add(testUser);
        await context.SaveChangesAsync();

        var loginService = new LoginService(context, _httpContextAccessor, _tokenService);
        var request = new LoginRequestSchema("driver@test.com", "WrongPassword123");

        var result = await loginService.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid credentials.", result.ErrorMessage);
        Assert.Null(result.Data);
    }

    [Fact(DisplayName = "Auth - Login Service: Should return success with valid JWT when credentials are valid")]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenCredentialsAreValid()
    {
        var options = CreateNewInMemoryDatabaseOptions();
        using var context = new AppDbContext(options);

        var testRole = new Role("ADMIN");
        var testUser = new User("Manager Alex", "admin@test.com", "SecurePassword789", testRole.Id);

        context.Roles.Add(testRole);
        context.Users.Add(testUser);
        await context.SaveChangesAsync();

        var loginService = new LoginService(context, _httpContextAccessor, _tokenService);
        var request = new LoginRequestSchema("admin@test.com", "SecurePassword789");

        var result = await loginService.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.Equal(testUser.Id, result.Data.UserId);
        Assert.Equal("ADMIN", result.Data.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
    }

    [Fact(DisplayName = "Auth - Login Endpoint: Should map and return 400 BadRequest when processing failed execution")]
    public async Task Endpoint_ShouldReturnBadRequest_WhenServiceExecutionFails()
    {
        var options = CreateNewInMemoryDatabaseOptions();
        using var context = new AppDbContext(options);

        var loginService = new LoginService(context, _httpContextAccessor, _tokenService);
        var request = new LoginRequestSchema("unknown@logistics.com", "AnyPassword");

        var result = await loginService.ExecuteAsync(request, CancellationToken.None);
        var httpResponseResult = !result.IsSuccess ? Results.BadRequest(result) : Results.Ok(result);

        Assert.NotNull(httpResponseResult);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiResult>>(httpResponseResult);
    }

    [Fact(DisplayName = "Auth - Login Service: Should return failure when user email does not exist in database")]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenEmailDoesNotExist()
    {
        var options = CreateNewInMemoryDatabaseOptions();
        using var context = new AppDbContext(options);

        var loginService = new LoginService(context, _httpContextAccessor, _tokenService);
        var request = new LoginRequestSchema("non-existent@northernroute.com", "Password123");

        var result = await loginService.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid credentials.", result.ErrorMessage);
        Assert.Null(result.Data);
    }

    [Fact(DisplayName = "Auth - Login Service: Should return failure when password hash is incorrect (Branch Validation)")]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenPasswordIsWrong()
    {
        var options = CreateNewInMemoryDatabaseOptions();
        using var context = new AppDbContext(options);

        var testRole = new Role("DRIVER");
        var testUser = new User("Alexandre Santos", "driver-branch@northernroute.com", "CorrectPassword123", testRole.Id);

        context.Roles.Add(testRole);
        context.Users.Add(testUser);
        await context.SaveChangesAsync();

        var loginService = new LoginService(context, _httpContextAccessor, _tokenService);
        var request = new LoginRequestSchema("driver-branch@northernroute.com", "WrongPassword123");

        var result = await loginService.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid credentials.", result.ErrorMessage);
        Assert.Null(result.Data);
    }

    [Fact(DisplayName = "Auth - Login Service: Should record a successful audit log entry when authentication succeeds")]
    public async Task ExecuteAsync_ShouldRecordAuditLog_WhenLoginIsSuccessful()
    {
        var options = CreateNewInMemoryDatabaseOptions();
        using var context = new AppDbContext(options);

        var testRole = new Role("ADMIN");
        var testUser = new User("Auditor Alex", "audit-check@test.com", "SecurePassword789", testRole.Id);

        context.Roles.Add(testRole);
        context.Users.Add(testUser);
        await context.SaveChangesAsync();

        var loginService = new LoginService(context, _httpContextAccessor, _tokenService);
        var request = new LoginRequestSchema("audit-check@test.com", "SecurePassword789");

        var result = await loginService.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        var savedAudit = await context.LoginAudits
            .FirstOrDefaultAsync(a => a.UserId == testUser.Id);

        Assert.NotNull(savedAudit);
        Assert.Equal("SUCCESS", savedAudit.Status);
        Assert.Equal("127.0.0.1", savedAudit.IpAddress);
        Assert.Equal("Xunit-Integration-Test-Environment-Agent", savedAudit.UserAgent);
    }

}
