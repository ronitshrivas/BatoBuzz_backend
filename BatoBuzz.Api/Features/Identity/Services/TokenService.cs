using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BatoBuzz.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BatoBuzz.Identity.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;
    public TokenService(IOptions<JwtSettings> jwt) => _jwt = jwt.Value;

    public (string token, DateTime expiresAt) CreateAccessToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public string CreateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public DateTime RefreshExpiry() => DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);
}