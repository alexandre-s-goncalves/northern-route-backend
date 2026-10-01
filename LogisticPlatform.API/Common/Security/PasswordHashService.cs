using System;
using System.Security.Cryptography;
using System.Text;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security.Contracts;
using Microsoft.AspNetCore.Identity;

namespace LogisticPlatform.API.Common.Security;

internal sealed class PasswordHashService(IPasswordHasher<User> passwordHasher) : IPasswordHashService
{
    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        PasswordVerificationResult verificationResult;
        try
        {
            verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (FormatException)
        {
            verificationResult = PasswordVerificationResult.Failed;
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, password));
            return true;
        }

        if (verificationResult == PasswordVerificationResult.Success)
        {
            return true;
        }

        var storedBytes = Encoding.UTF8.GetBytes(user.PasswordHash);
        var providedBytes = Encoding.UTF8.GetBytes(password);
        if (!CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes))
        {
            return false;
        }

        user.SetPasswordHash(passwordHasher.HashPassword(user, password));
        return true;
    }
}
