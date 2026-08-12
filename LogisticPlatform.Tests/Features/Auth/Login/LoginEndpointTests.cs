using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using Xunit;

using ApiResult = LogisticPlatform.API.Common.ResultSchema<LogisticPlatform.API.Features.Auth.Login.Schemas.LoginResponseSchema>;

namespace LogisticPlatform.Tests.Features.Auth.Login;

public sealed class LoginEndpointTests : IClassFixture<WebTestFixture>
{
    private readonly HttpClient _client;
    private readonly WebTestFixture _factory;

    public LoginEndpointTests(WebTestFixture factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Auth - Login Endpoint: Should return HTTP 200 OK with valid JWT when credentials are perfect")]
    public async Task Login_ShouldReturnOk_WhenCredentialsAreValid()
    {
        // Arrange 
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existingUser = await context.Users
                .FirstOrDefaultAsync(u => u.Email == "driver@northernroute.com");

            if (existingUser is null)
            {
                var defaultRole = await context.Roles.FirstOrDefaultAsync();
                Guid roleId;

                if (defaultRole is null)
                {
                    var mockRole = new Role("DRIVER");
                    context.Roles.Add(mockRole);
                    await context.SaveChangesAsync();

                    roleId = mockRole.Id;
                }
                else
                {
                    roleId = defaultRole.Id;
                }

                var testUser = new User(
                    "Alexandre Santos",
                    "driver@northernroute.com",
                    "SecurePassword123",
                    roleId
                );

                context.Users.Add(testUser);
                await context.SaveChangesAsync();
            }
        }

        var requestPayload = new LoginRequestSchema("driver@northernroute.com", "SecurePassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", requestPayload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var resultBody = await response.Content.ReadFromJsonAsync<ApiResult>();

        Assert.NotNull(resultBody);
        Assert.True(resultBody.IsSuccess);
        Assert.NotNull(resultBody.Data);
        Assert.NotNull(resultBody.Data.Token);
    }

    [Fact(DisplayName = "Auth - Login Endpoint: Should return HTTP 400 BadRequest when credentials are completely invalid")]
    public async Task Login_ShouldReturnBadRequest_WhenCredentialsAreInvalid()
    {
        // Arrange
        var requestPayload = new LoginRequestSchema("invalid-driver@northernroute.com", "WrongPassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", requestPayload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var resultBody = await response.Content.ReadFromJsonAsync<ApiResult>();

        Assert.NotNull(resultBody);
        Assert.False(resultBody.IsSuccess);
        Assert.Equal("Invalid credentials.", resultBody.ErrorMessage);
        Assert.Null(resultBody.Data);
    }
}
