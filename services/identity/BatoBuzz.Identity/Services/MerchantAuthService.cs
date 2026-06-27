using System.Security.Claims;
using BatoBuzz.Identity.Data;
using BatoBuzz.Identity.Dtos.Auth;
using BatoBuzz.Identity.Dtos.Merchant;
using BatoBuzz.Identity.Entities;
using BatoBuzz.Identity.Enums;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Identity.Services;

public sealed class MerchantAuthService : IMerchantAuthService
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public MerchantAuthService(IdentityDbContext db, IPasswordHasher hasher, ITokenService tokens)
        => (_db, _hasher, _tokens) = (db, hasher, tokens);

    // Normalize Nepal numbers: strip spaces, drop +977 / 977 prefix to a 10-digit local form.
    private static string Normalize(string phone)
    {
        var p = phone.Trim().Replace(" ", "").Replace("-", "");
        if (p.StartsWith("+977")) p = p[4..];
        else if (p.StartsWith("977") && p.Length > 10) p = p[3..];
        return p;
    }

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct)
    {
        var p = Normalize(phone);
        return _db.Merchants.AnyAsync(m => m.Phone == p, ct);
    }

    public async Task<AuthResponse> SignupAsync(MerchantSignupRequest req, CancellationToken ct)
    {
        var phone = Normalize(req.Phone);
        if (await _db.Merchants.AnyAsync(m => m.Phone == phone, ct))
            throw AppException.Conflict("This phone number is already registered.");

        var merchant = new MerchantAccount
        {
            Phone = phone,
            PinHash = _hasher.Hash(req.Pin),
            BusinessName = req.BusinessName.Trim(),
            OwnerPhotoUrl = req.OwnerPhotoUrl,
            Status = MerchantStatus.Pending,   // gate: cannot enter app until approved
        };
        _db.Merchants.Add(merchant);
        await _db.SaveChangesAsync(ct);
        return await IssueAsync(merchant, ct);
    }

    public async Task<AuthResponse> LoginAsync(MerchantLoginRequest req, CancellationToken ct)
    {
        var phone = Normalize(req.Phone);
        var merchant = await _db.Merchants.FirstOrDefaultAsync(m => m.Phone == phone, ct)
                       ?? throw AppException.Unauthorized("Invalid phone or PIN.");

        if (!_hasher.Verify(req.Pin, merchant.PinHash))
            throw AppException.Unauthorized("Invalid phone or PIN.");

        // We still issue a token for Pending/Rejected merchants so the app can
        // read the status and show the "Under Review" / "Rejected" screen.
        // Approval is enforced per-endpoint via the ApprovedMerchant policy.
        return await IssueAsync(merchant, ct);
    }

    private async Task<AuthResponse> IssueAsync(MerchantAccount m, CancellationToken ct)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, m.Id.ToString()),
            new(ClaimTypes.MobilePhone, m.Phone),
            new(ClaimTypes.Role, AppRoles.Merchant),
            new(TokenClaims.AccountType, AppRoles.Merchant),
            new(TokenClaims.MerchantStatus, m.Status.ToString().ToLowerInvariant()),
        };
        var (access, accessExp) = _tokens.CreateAccessToken(claims);

        var refresh = new RefreshToken
        {
            Token = _tokens.CreateRefreshTokenValue(),
            ExpiresAt = _tokens.RefreshExpiry(),
            MerchantAccountId = m.Id,
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        var profile = new MerchantProfileDto(m.Id, m.Phone, m.BusinessName,
            m.OwnerPhotoUrl, m.Status.ToString().ToLowerInvariant(), m.CreatedAt);

        return new AuthResponse(access, refresh.Token, accessExp, AppRoles.Merchant, profile);
    }
}