using System.Security.Claims;
using BatoBuzz.Identity.Data;
using BatoBuzz.Identity.Dtos.Auth;
using BatoBuzz.Identity.Dtos.User;
using BatoBuzz.Identity.Entities;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Identity.Services;

public sealed class UserAuthService : IUserAuthService
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IGoogleAuthValidator _google;

    public UserAuthService(IdentityDbContext db, IPasswordHasher hasher,
        ITokenService tokens, IGoogleAuthValidator google)
        => (_db, _hasher, _tokens, _google) = (db, hasher, tokens, google);

    public async Task<AuthResponse> RegisterAsync(UserRegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw AppException.Conflict("An account with this email already exists.");

        var user = new AppUser
        {
            Email = email,
            DisplayName = req.DisplayName.Trim(),
            Phone = req.Phone?.Trim(),
            PasswordHash = _hasher.Hash(req.Password),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(UserLoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
                   ?? throw AppException.Unauthorized("Invalid email or password.");

        if (user.PasswordHash is null || !_hasher.Verify(req.Password, user.PasswordHash))
            throw AppException.Unauthorized("Invalid email or password.");

        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest req, CancellationToken ct)
    {
        var info = await _google.ValidateAsync(req.IdToken, ct);
        var email = info.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.GoogleSubjectId == info.Subject || u.Email == email, ct);

        if (user is null)
        {
            user = new AppUser
            {
                Email = email,
                DisplayName = info.Name ?? email.Split('@')[0],
                PhotoUrl = info.Picture,
                GoogleSubjectId = info.Subject,
                EmailVerified = true,
            };
            _db.Users.Add(user);
        }
        else
        {
            user.GoogleSubjectId ??= info.Subject;
            if (string.IsNullOrWhiteSpace(user.PhotoUrl)) user.PhotoUrl = info.Picture;
            if (string.IsNullOrWhiteSpace(user.DisplayName)) user.DisplayName = info.Name ?? user.DisplayName;
            user.EmailVerified = true;
        }
        await _db.SaveChangesAsync(ct);
        return await IssueAsync(user, ct);
    }

    private async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, AppRoles.User),
            new(TokenClaims.AccountType, AppRoles.User),
        };
        var (access, accessExp) = _tokens.CreateAccessToken(claims);

        var refresh = new RefreshToken
        {
            Token = _tokens.CreateRefreshTokenValue(),
            ExpiresAt = _tokens.RefreshExpiry(),
            AppUserId = user.Id,
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        var profile = new UserProfileDto(user.Id, user.DisplayName, user.Email,
            user.Phone, user.PhotoUrl, user.EmailVerified, user.CreatedAt);

        return new AuthResponse(access, refresh.Token, accessExp, AppRoles.User, profile);
    }
}