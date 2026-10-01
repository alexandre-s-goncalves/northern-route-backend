using LogisticPlatform.API.Common.Domain;

namespace LogisticPlatform.API.Common.Security;

internal interface IPasswordHashService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string password);
}
