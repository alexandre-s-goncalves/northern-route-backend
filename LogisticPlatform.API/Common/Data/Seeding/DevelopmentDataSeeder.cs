using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Common.Data.Seeding;

internal sealed class DevelopmentDataSeeder(AppDbContext dbContext, IPasswordHasher<User> passwordHasher)
{
    public async Task SeedAsync(bool seedDevelopmentData, CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.Roles.ToListAsync(cancellationToken);
        var seededEmails = DevelopmentSeedData.Users
            .SelectMany(seed => new[] { seed.Email, seed.Email.ToUpperInvariant() })
            .ToArray();
        var users = await dbContext.Users
            .Where(user => seededEmails.Contains(user.Email))
            .ToListAsync(cancellationToken);

        if (seedDevelopmentData)
        {
            foreach (var roleName in DevelopmentSeedData.Roles)
            {
                if (roles.Any(role => string.Equals(role.Name, roleName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var role = new Role(roleName);
                dbContext.Roles.Add(role);
                roles.Add(role);
            }
        }

        foreach (var userSeed in DevelopmentSeedData.Users)
        {
            var user = users.FirstOrDefault(candidate =>
                string.Equals(candidate.Email.Trim(), userSeed.Email, StringComparison.OrdinalIgnoreCase));

            if (user is null)
            {
                if (!seedDevelopmentData)
                {
                    continue;
                }

                var role = roles.First(candidate =>
                    string.Equals(candidate.Name, userSeed.RoleName, StringComparison.OrdinalIgnoreCase));
                user = new User(userSeed.Name, userSeed.Email, string.Empty, role.Id);
                user.SetPasswordHash(passwordHasher.HashPassword(user, userSeed.Password));
                dbContext.Users.Add(user);
                users.Add(user);
                continue;
            }

            NormalizeSeededPassword(user, userSeed.Password);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private void NormalizeSeededPassword(User user, string plainPassword)
    {
        if (string.Equals(user.PasswordHash, plainPassword, StringComparison.Ordinal))
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, plainPassword));
            return;
        }

        PasswordVerificationResult verificationResult;
        try
        {
            verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, plainPassword);
        }
        catch (FormatException)
        {
            return;
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, plainPassword));
        }
    }
}
