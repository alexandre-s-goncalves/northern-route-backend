using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using LogisticPlatform.API.Features.Auth.Refresh.Schemas;
using LogisticPlatform.API.Features.Auth.Refresh.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.Refresh;

public sealed class RefreshServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly ITokenService _tokenService;

    public RefreshServiceTests()
    {
        var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
        {
            { "JWT_SECRET_KEY", "SuperSecretSecureKeyForNorthernRouteLogistics2026" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _context = new AppDbContext(CreateNewInMemoryDatabaseOptions());
        _refreshTokenService = new RefreshTokenService(_context, TimeProvider.System);
        _tokenService = new TokenService(configuration);
    }

    private static DbContextOptions<AppDbContext> CreateNewInMemoryDatabaseOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact(DisplayName = "Auth - Refresh Service: Should throw when request is null")]
    public async Task ExecuteAsync_ShouldThrow_WhenRequestIsNull()
    {
        var service = new RefreshService(_context, _refreshTokenService, _tokenService);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExecuteAsync(null!, CancellationToken.None));
    }

    [Fact(DisplayName = "Auth - Refresh Service: Should fail when refresh token is invalid")]
    public async Task ExecuteAsync_ShouldFail_WhenTokenIsInvalid()
    {
        var service = new RefreshService(_context, _refreshTokenService, _tokenService);

        var result = await service.ExecuteAsync(new RefreshRequestSchema("invalid-token"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid, expired, or revoked refresh token.", result.ErrorMessage);
        Assert.Null(result.Data);
    }

    [Fact(DisplayName = "Auth - Refresh Service: Should fail when user referenced by token no longer exists")]
    public async Task ExecuteAsync_ShouldFail_WhenUserIsMissing()
    {
        var missingUserId = Guid.NewGuid();
        var deviceSessionId = Guid.NewGuid();
        var refreshToken = await _refreshTokenService.CreateTokenAsync(missingUserId, deviceSessionId, CancellationToken.None);

        var service = new RefreshService(_context, _refreshTokenService, _tokenService);
        var result = await service.ExecuteAsync(new RefreshRequestSchema(refreshToken), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User reference associated with token not found.", result.ErrorMessage);
        Assert.Null(result.Data);
    }

    [Fact(DisplayName = "Auth - Refresh Service: Should return new access and refresh tokens when session is valid")]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenTokenIsValid()
    {
        var role = new Role("ADMIN");
        var user = new User("Refresh User", "refresh@test.com", "Password123", role.Id);
        _context.Roles.Add(role);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = await _refreshTokenService.CreateTokenAsync(user.Id, Guid.NewGuid(), CancellationToken.None);

        var service = new RefreshService(_context, _refreshTokenService, _tokenService);
        var result = await service.ExecuteAsync(new RefreshRequestSchema(token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));
        Assert.NotEqual(token, result.Data.RefreshToken);

        var sessions = await _context.RefreshTokenSessions.AsNoTracking().ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.IsRevoked);
        Assert.Contains(sessions, session => !session.IsRevoked);
    }
}

public sealed class RefreshTokenServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RefreshTokenService _service;

    public RefreshTokenServiceTests()
    {
        _context = new AppDbContext(CreateNewInMemoryDatabaseOptions());
        _service = new RefreshTokenService(_context, TimeProvider.System);
    }

    private static DbContextOptions<AppDbContext> CreateNewInMemoryDatabaseOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact(DisplayName = "Auth - Refresh Token Service: Should create and persist a refresh token session")]
    public async Task CreateTokenAsync_ShouldPersistSessionAndReturnToken()
    {
        var userId = Guid.NewGuid();
        var deviceSessionId = Guid.NewGuid();

        var token = await _service.CreateTokenAsync(userId, deviceSessionId, CancellationToken.None);

        var persistedSession = await _context.RefreshTokenSessions.SingleAsync();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(userId, persistedSession.UserId);
        Assert.Equal(deviceSessionId, persistedSession.DeviceSessionId);
        Assert.False(persistedSession.IsRevoked);
        Assert.False(string.IsNullOrWhiteSpace(persistedSession.TokenHash));
    }

    [Fact(DisplayName = "Auth - Refresh Token Service: Should return null for whitespace token")]
    public async Task ValidateAndRotateTokenAsync_ShouldReturnNull_WhenTokenIsWhitespace()
    {
        var result = await _service.ValidateAndRotateTokenAsync("   ", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact(DisplayName = "Auth - Refresh Token Service: Should return null when refresh token is expired or revoked")]
    public async Task ValidateAndRotateTokenAsync_ShouldReturnNull_WhenTokenIsExpiredOrRevoked()
    {
        var userId = Guid.NewGuid();
        var deviceSessionId = Guid.NewGuid();
        var token = await _service.CreateTokenAsync(userId, deviceSessionId, CancellationToken.None);

        var session = await _context.RefreshTokenSessions.SingleAsync();
        session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();

        var expiredResult = await _service.ValidateAndRotateTokenAsync(token, CancellationToken.None);
        Assert.Null(expiredResult);

        session.ExpiresAt = DateTime.UtcNow.AddDays(7);
        session.IsRevoked = true;
        await _context.SaveChangesAsync();

        var revokedResult = await _service.ValidateAndRotateTokenAsync(token, CancellationToken.None);
        Assert.Null(revokedResult);
    }

    [Fact(DisplayName = "Auth - Refresh Token Service: Should rotate an active session when token is valid")]
    public async Task ValidateAndRotateTokenAsync_ShouldRotateValidSession()
    {
        var userId = Guid.NewGuid();
        var deviceSessionId = Guid.NewGuid();
        var token = await _service.CreateTokenAsync(userId, deviceSessionId, CancellationToken.None);

        var result = await _service.ValidateAndRotateTokenAsync(token, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsRevoked);
        Assert.NotNull(result.UpdatedAt);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(deviceSessionId, result.DeviceSessionId);
    }
}
