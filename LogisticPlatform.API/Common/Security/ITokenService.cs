using LogisticPlatform.API.Common.Domain;

namespace LogisticPlatform.API.Common.Security;

internal interface ITokenService
{
    string GenerateToken(User user);
}
