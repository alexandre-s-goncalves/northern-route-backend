using LogisticPlatform.API.Common.Domain;

namespace LogisticPlatform.API.Common.Security.Contracts;

internal interface ITokenService
{
    string GenerateToken(User user);
}
