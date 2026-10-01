using System;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Data.Seeding;
using LogisticPlatform.API.Common.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogisticPlatform.Tests.Common.Data.Seeding;

public sealed class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_ShouldCreateDevelopmentDataAndHashPasswords_Idempotently()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var seeder = new DevelopmentDataSeeder(context, new PasswordHasher<User>());
        await seeder.SeedAsync(seedDevelopmentData: true);
        var initialUserCount = await context.Users.CountAsync();
        var seededUser = await context.Users.SingleAsync(user => user.Email == "ALE@ALE.COM");
        var passwordHasher = new PasswordHasher<User>();

        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Equal(2, initialUserCount);
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(seededUser, seededUser.PasswordHash, "Password123"));

        await seeder.SeedAsync(seedDevelopmentData: true);

        Assert.Equal(initialUserCount, await context.Users.CountAsync());
        Assert.Equal(2, await context.Roles.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_ShouldNotCreateDevelopmentDataOutsideDevelopment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var seeder = new DevelopmentDataSeeder(context, new PasswordHasher<User>());
        await seeder.SeedAsync(seedDevelopmentData: false);

        Assert.Empty(await context.Roles.ToListAsync());
        Assert.Empty(await context.Users.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_ShouldUpgradeLegacyLowercaseEmailWithoutCreatingDuplicateUser()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var legacyUser = new User("Alexandre Santos", "ale@ale.com", "Password123", Guid.NewGuid());
        context.Users.Add(legacyUser);
        context.Entry(legacyUser).Property(user => user.Email).CurrentValue = "ale@ale.com";
        await context.SaveChangesAsync();

        var seeder = new DevelopmentDataSeeder(context, new PasswordHasher<User>());
        await seeder.SeedAsync(seedDevelopmentData: true);

        Assert.Single(await context.Users
            .Where(user => user.Email == "ale@ale.com" || user.Email == "ALE@ALE.COM")
            .ToListAsync());
        Assert.NotEqual("Password123", legacyUser.PasswordHash);
    }
}
