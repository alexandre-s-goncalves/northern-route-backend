using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Common.Security.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.Login;

public sealed class PasswordHashServiceTests
{
    private readonly PasswordHasher<User> _passwordHasher = new(Options.Create(new PasswordHasherOptions()));
    private readonly PasswordHashService _service;

    public PasswordHashServiceTests()
    {
        _service = new PasswordHashService(_passwordHasher);
    }

    [Fact]
    public void HashPassword_ShouldCreateHash_VerifiableForUser()
    {
        var user = CreateUser("unused");

        var hash = _service.HashPassword(user, "SecurePassword123");
        user.SetPasswordHash(hash);

        Assert.NotEqual("SecurePassword123", hash);
        Assert.True(_service.VerifyPassword(user, "SecurePassword123"));
    }

    [Fact]
    public void VerifyPassword_ShouldUpgradeLegacyPlaintextHash()
    {
        var user = CreateUser("LegacyPassword123");

        var verified = _service.VerifyPassword(user, "LegacyPassword123");

        Assert.True(verified);
        Assert.NotEqual("LegacyPassword123", user.PasswordHash);
        Assert.True(_service.VerifyPassword(user, "LegacyPassword123"));
    }

    [Fact]
    public void VerifyPassword_ShouldRejectIncorrectPassword_WithoutChangingHash()
    {
        var user = CreateUser("LegacyPassword123");
        var originalHash = user.PasswordHash;

        var verified = _service.VerifyPassword(user, "WrongPassword123");

        Assert.False(verified);
        Assert.Equal(originalHash, user.PasswordHash);
    }

    [Fact]
    public void VerifyPassword_ShouldRehashWhenHasherRequestsUpgrade()
    {
        var legacyHasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        }));
        var user = CreateUser(legacyHasher.HashPassword(CreateUser("unused"), "SecurePassword123"));
        var legacyHash = user.PasswordHash;

        var verified = _service.VerifyPassword(user, "SecurePassword123");

        Assert.True(verified);
        Assert.NotEqual(legacyHash, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "SecurePassword123"));
    }

    [Fact]
    public void HashPassword_ShouldThrow_WhenUserIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.HashPassword(null!, "password"));
    }

    [Fact]
    public void HashPassword_ShouldThrow_WhenPasswordIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _service.HashPassword(CreateUser("unused"), " "));
    }

    [Fact]
    public void VerifyPassword_ShouldThrow_WhenUserIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.VerifyPassword(null!, "password"));
    }

    [Fact]
    public void VerifyPassword_ShouldThrow_WhenPasswordIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _service.VerifyPassword(CreateUser("unused"), " "));
    }

    private static User CreateUser(string passwordHash)
    {
        return new User("Password Test", $"password-{Guid.NewGuid():N}@example.com", passwordHash, Guid.NewGuid());
    }
}
