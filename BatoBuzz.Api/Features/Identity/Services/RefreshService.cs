// Services/RefreshService.cs
using System.Security.Claims;
using BatoBuzz.Identity.Data;
using BatoBuzz.Identity.Dtos.Auth;
using BatoBuzz.Identity.Dtos.Merchant;
using BatoBuzz.Identity.Dtos.User;
using BatoBuzz.Identity.Entities;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Identity.Services;

public sealed class RefreshService : IRefreshService
{
    private readonly IdentityDbContext _db;
    private readonly ITokenService _tokens;

    public RefreshService(IdentityDbContext db, ITokenService tokens)
        => (_db, _tokens) = (db, tokens);

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var existing = await _db.RefreshTokens
            .Include(r => r.AppUser)
            .Include(r => r.MerchantAccount)
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct)
            ?? throw AppException.Unauthorized("Invalid refresh token.");

        if (!existing.IsActive)
            throw AppException.Unauthorized("Refresh token expired or revoked.");

        // Rotate: revoke old, issue new.
        existing.RevokedAt = DateTime.UtcNow;

        if (existing.AppUser is { } user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, AppRoles.User),
                new(TokenClaims.AccountType, AppRoles.User),
                new(TokenClaims.DisplayName, user.DisplayName),
            };
            if (!string.IsNullOrWhiteSpace(user.PhotoUrl))
                claims.Add(new Claim(TokenClaims.PhotoUrl, user.PhotoUrl));
            var (access, exp) = _tokens.CreateAccessToken(claims);
            var newRt = NewToken(appUserId: user.Id);
            _db.RefreshTokens.Add(newRt);
            await _db.SaveChangesAsync(ct);

            var profile = new UserProfileDto(user.Id, user.DisplayName, user.Email,
                user.Phone, user.PhotoUrl, user.EmailVerified, user.CreatedAt);
            return new AuthResponse(access, newRt.Token, exp, AppRoles.User, profile);
        }

        if (existing.MerchantAccount is { } m)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, m.Id.ToString()),
                new(ClaimTypes.MobilePhone, m.Phone),
                new(ClaimTypes.Role, AppRoles.Merchant),
                new(TokenClaims.AccountType, AppRoles.Merchant),
                new(TokenClaims.MerchantStatus, m.Status.ToString().ToLowerInvariant()),
                new(TokenClaims.DisplayName, m.BusinessName),
            };
            if (!string.IsNullOrWhiteSpace(m.OwnerPhotoUrl))
                claims.Add(new Claim(TokenClaims.PhotoUrl, m.OwnerPhotoUrl));
            var (access, exp) = _tokens.CreateAccessToken(claims);
            var newRt = NewToken(merchantId: m.Id);
            _db.RefreshTokens.Add(newRt);
            await _db.SaveChangesAsync(ct);

            var profile = new MerchantProfileDto(m.Id, m.Phone, m.BusinessName,
                m.OwnerPhotoUrl, m.Status.ToString().ToLowerInvariant(), m.CreatedAt);
            return new AuthResponse(access, newRt.Token, exp, AppRoles.Merchant, profile);
        }

        throw AppException.Unauthorized("Orphaned refresh token.");
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var t = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken, ct);
        if (t is { RevokedAt: null }) { t.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
    }

    private RefreshToken NewToken(Guid? appUserId = null, Guid? merchantId = null) => new()
    {
        Token = _tokens.CreateRefreshTokenValue(),
        ExpiresAt = _tokens.RefreshExpiry(),
        AppUserId = appUserId,
        MerchantAccountId = merchantId,
    };
}