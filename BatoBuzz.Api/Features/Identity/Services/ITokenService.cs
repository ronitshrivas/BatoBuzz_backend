using System.Security.Claims;
using BatoBuzz.Identity.Entities;

namespace BatoBuzz.Identity.Services;

public interface ITokenService
{
    (string token, DateTime expiresAt) CreateAccessToken(IEnumerable<Claim> claims);
    string CreateRefreshTokenValue();
    DateTime RefreshExpiry();
}