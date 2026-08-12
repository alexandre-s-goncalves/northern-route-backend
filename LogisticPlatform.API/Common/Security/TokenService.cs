using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using LogisticPlatform.API.Common.Domain;

namespace LogisticPlatform.API.Common.Security;

internal sealed class TokenService(IConfiguration configuration) : ITokenService
{
    public string GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var secret = configuration["JWT_SECRET_KEY"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Security failure: JWT Secret Key is missing from the environment configurations.");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "USER")
            ]),
            Expires = DateTime.UtcNow.AddHours(8),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
