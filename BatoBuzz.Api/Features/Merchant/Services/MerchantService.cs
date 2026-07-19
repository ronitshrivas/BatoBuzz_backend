using BatoBuzz.Merchant.Data;
using BatoBuzz.Merchant.Dtos;
using BatoBuzz.Merchant.Entities;
using BatoBuzz.Merchant.Enums;
using BatoBuzz.Merchant.Extensions;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Merchant.Services;

public sealed class MerchantService : IMerchantService
{
    private readonly MerchantDbContext _db;
    private readonly ICurrentActor _actor;
    private readonly IFileStorage _files;

    public MerchantService(MerchantDbContext db, ICurrentActor actor, IFileStorage files)
        => (_db, _actor, _files) = (db, actor, files);

    /// Saves the business profile + KYC for the signed-in merchant.
    ///
    /// This is step 2 of registration: the app has already created the auth
    /// record in Identity (phone + PIN) and is now holding that merchant's
    /// token. So the identity comes from the JWT — never the request body —
    /// which is what stops one merchant from writing another's profile.
    public async Task<MerchantProfileDto> CreateAsync(CreateMerchantProfileRequest req, CancellationToken ct)
    {
        var merchantId = _actor.Id;
        if (!_actor.IsMerchant)
            throw AppException.Forbidden("Only merchant accounts can create a business profile.");

        if (await _db.Merchants.AnyAsync(m => m.MerchantId == merchantId, ct))
            throw AppException.Conflict("This merchant already has a profile.");

        var categories = req.BusinessCategories
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToList();

        var profile = new MerchantProfile
        {
            MerchantId = merchantId,
            Phone = _actor.Phone,
            BusinessName = req.BusinessName.Trim(),
            BusinessEmail = req.BusinessEmail.Trim().ToLowerInvariant(),
            BusinessCategories = categories,
            BusinessCategory = string.Join(", ", categories),
            BusinessAddress = req.BusinessAddress.Trim(),
            BusinessPanNumber = req.BusinessPanNumber.Trim(),
            CityId = req.CityId.Trim(),
            CityName = req.CityName.Trim(),
            Ward = req.Ward.Trim(),
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            Status = MerchantStatus.Pending,
        };

        // KYC files are named deterministically per merchant, so a re-upload
        // overwrites rather than piling up — same as the old
        // merchant_kyc/{uid}/{doc}.jpg layout in Firebase Storage.
        var folder = Path.Combine("merchant_kyc", merchantId.ToString());
        profile.CitizenshipFrontUrl = await SaveIfPresent(req.CitizenshipFront, folder, "citizenship_front", ct);
        profile.CitizenshipBackUrl = await SaveIfPresent(req.CitizenshipBack, folder, "citizenship_back", ct);
        profile.PanCardUrl = await SaveIfPresent(req.PanCard, folder, "pan_card", ct);
        profile.OwnerPhotoUrl = await SaveIfPresent(req.OwnerPhoto, folder, "owner_photo", ct);

        _db.Merchants.Add(profile);
        await _db.SaveChangesAsync(ct);

        return profile.ToDto();
    }

    public async Task<MerchantProfileDto> GetMineAsync(CancellationToken ct)
    {
        var profile = await _db.Merchants.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MerchantId == _actor.Id, ct)
            ?? throw AppException.NotFound("You haven't created a business profile yet.");
        return profile.ToDto();
    }

    public async Task<MerchantProfileDto> GetByIdAsync(Guid merchantId, CancellationToken ct)
    {
        var profile = await _db.Merchants.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MerchantId == merchantId, ct)
            ?? throw AppException.NotFound("Merchant not found.");
        return profile.ToDto();
    }

    /// The single bit the app's login gate needs — cheap to poll on app start.
    public async Task<MerchantStatusDto> GetMyStatusAsync(CancellationToken ct)
    {
        var row = await _db.Merchants.AsNoTracking()
            .Where(m => m.MerchantId == _actor.Id)
            .Select(m => new { m.Status, m.RejectionReason })
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("You haven't created a business profile yet.");

        return new MerchantStatusDto(_actor.Id, row.Status.ToString().ToLowerInvariant(), row.RejectionReason);
    }

    public async Task<MerchantProfileDto> UpdateAsync(UpdateMerchantProfileRequest req, CancellationToken ct)
    {
        var profile = await _db.Merchants.FirstOrDefaultAsync(m => m.MerchantId == _actor.Id, ct)
            ?? throw AppException.NotFound("You haven't created a business profile yet.");

        if (req.BusinessName is { } bn) profile.BusinessName = bn.Trim();
        if (req.BusinessEmail is { } be) profile.BusinessEmail = be.Trim().ToLowerInvariant();
        if (req.BusinessAddress is { } ba) profile.BusinessAddress = ba.Trim();
        if (req.BusinessPanNumber is { } pan) profile.BusinessPanNumber = pan.Trim();
        if (req.CityId is { } cid) profile.CityId = cid.Trim();
        if (req.CityName is { } cn) profile.CityName = cn.Trim();
        if (req.Ward is { } w) profile.Ward = w.Trim();
        if (req.Latitude is { } lat) profile.Latitude = lat;
        if (req.Longitude is { } lng) profile.Longitude = lng;

        if (req.BusinessCategories is { } cats)
        {
            var clean = cats.Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
            profile.BusinessCategories = clean;
            profile.BusinessCategory = string.Join(", ", clean);
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return profile.ToDto();
    }

    // ── Admin ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MerchantProfileDto>> ListAsync(string? status, CancellationToken ct)
    {
        RequireAdmin();

        var q = _db.Merchants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<MerchantStatus>(status, ignoreCase: true, out var parsed))
                throw new AppException("Unknown status. Expected: pending, approved, or rejected.");
            q = q.Where(m => m.Status == parsed);
        }

        var rows = await q.OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
        return rows.Select(m => m.ToDto()).ToList();
    }

    /// Approve or reject. This flips the profile's status; the app's gate polls
    /// `GetMyStatusAsync`. (Identity holds a parallel gate on the auth record —
    /// see the README for keeping the two in sync via the admin panel.)
    public async Task<MerchantProfileDto> ReviewAsync(Guid merchantId, ReviewMerchantRequest req, CancellationToken ct)
    {
        RequireAdmin();

        var profile = await _db.Merchants.FirstOrDefaultAsync(m => m.MerchantId == merchantId, ct)
            ?? throw AppException.NotFound("Merchant not found.");

        if (req.Approve)
        {
            profile.Status = MerchantStatus.Approved;
            profile.RejectionReason = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(req.RejectionReason))
                throw new AppException("A rejection reason is required when rejecting a merchant.");
            profile.Status = MerchantStatus.Rejected;
            profile.RejectionReason = req.RejectionReason.Trim();
        }

        profile.ReviewedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return profile.ToDto();
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private void RequireAdmin()
    {
        if (!_actor.IsAdmin)
            throw AppException.Forbidden("Admin access required.");
    }

    private async Task<string?> SaveIfPresent(IFormFile? file, string folder, string name, CancellationToken ct)
        => file is null ? null : await _files.SaveAsync(file, folder, name, ct);
}
