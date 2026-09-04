using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        var email = $"driver_{Guid.NewGuid().ToString()[..8]}@northernroute.com";
        var password = "SecurePassword123";

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var mockRole = new Role($"DRIVER_{Guid.NewGuid().ToString()[..4]}");
            context.Roles.Add(mockRole);
            await context.SaveChangesAsync();

            var testUser = new User(
                "Alexandre Santos",
                email,
                password,
                mockRole.Id
            );

            context.Users.Add(testUser);
            await context.SaveChangesAsync();
        }

        var requestPayload = new LoginRequestSchema(email, password);

        var response = await _client.PostAsJsonAsync("/api/auth/login", requestPayload);

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
        var email = $"invalid_driver_{Guid.NewGuid().ToString()[..8]}@northernroute.com";
        var password = "WrongPassword123";

        var requestPayload = new LoginRequestSchema(email, password);

        var response = await _client.PostAsJsonAsync("/api/auth/login", requestPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var resultBody = await response.Content.ReadFromJsonAsync<ApiResult>();

        Assert.NotNull(resultBody);
        Assert.False(resultBody.IsSuccess);
        Assert.Equal("Invalid credentials.", resultBody.ErrorMessage);
        Assert.Null(resultBody.Data);
    }
}
