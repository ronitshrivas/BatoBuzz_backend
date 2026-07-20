using BatoBuzz.Merchant.Services;              // reuse ICurrentActor + IFileStorage
using BatoBuzz.ServiceProvider.Data;
using BatoBuzz.ServiceProvider.Dtos;
using BatoBuzz.ServiceProvider.Entities;
using BatoBuzz.ServiceProvider.Enums;
using BatoBuzz.ServiceProvider.Extensions;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.ServiceProvider.Services;

public interface IServiceProviderService
{
    Task<ProviderDto> SubmitAsync(SubmitApplicationRequest req, CancellationToken ct);
    Task<ProviderDto> GetMineAsync(CancellationToken ct);
    Task<ProviderStatusDto> GetMyStatusAsync(CancellationToken ct);
    Task<ProviderDto> GetByIdAsync(Guid providerId, CancellationToken ct);
    Task<IReadOnlyList<ProviderDto>> ListApprovedAsync(string? category, string? search, CancellationToken ct);

    // Reviews
    Task<IReadOnlyList<ReviewDto>> GetReviewsAsync(Guid providerId, CancellationToken ct);
    Task<ReviewDto> UpsertReviewAsync(Guid providerId, UpsertReviewRequest req, CancellationToken ct);
    Task DeleteReviewAsync(Guid providerId, CancellationToken ct);

    // Admin
    Task<IReadOnlyList<ProviderDto>> AdminListAsync(string? status, CancellationToken ct);
    Task<ProviderDto> ReviewApplicationAsync(Guid providerId, ReviewProviderRequest req, CancellationToken ct);
}

public sealed class ServiceProviderService : IServiceProviderService
{
    private readonly ServiceProviderDbContext _db;
    private readonly ICurrentActor _actor;
    private readonly IFileStorage _files;

    public ServiceProviderService(ServiceProviderDbContext db, ICurrentActor actor, IFileStorage files)
        => (_db, _actor, _files) = (db, actor, files);

    /// A signed-in user applies to become a provider. Identity comes from the
    /// JWT, so one user maps to at most one application (unique index enforces
    /// it). Photo + verification document upload to disk when present, both
    /// optional — matching the app's nullable File paths.
    public async Task<ProviderDto> SubmitAsync(SubmitApplicationRequest req, CancellationToken ct)
    {
        var userId = _actor.Id;

        if (await _db.Providers.AnyAsync(p => p.SubmittedById == userId, ct))
            throw AppException.Conflict("You've already submitted an application.");

        var categories = req.ServiceCategories.Select(c => c.Trim()).Where(c => c.Length > 0).ToList();

        var provider = new ServiceProviderEntity
        {
            SubmittedById = userId,
            FullName = req.FullName.Trim(),
            Profession = req.Profession.Trim(),
            Phone = req.Phone.Trim(),
            WhatsApp = req.WhatsApp.Trim(),
            ServiceArea = req.ServiceArea.Trim(),
            Experience = EnumMapping.ToExperience(req.Experience),
            ServiceCategories = categories,
            About = req.About.Trim(),
            AvailableNow = req.AvailableNow,
            Status = ProviderStatus.Pending,
        };

        var folder = Path.Combine("service_providers", userId.ToString());
        if (req.Photo is not null)
            provider.PhotoUrl = await _files.SaveAsync(req.Photo, folder, "photo", ct);
        if (req.Document is not null)
            provider.DocumentUrl = await _files.SaveAsync(req.Document, folder, "document", ct);

        _db.Providers.Add(provider);
        await _db.SaveChangesAsync(ct);
        return provider.ToDto();
    }

    public async Task<ProviderDto> GetMineAsync(CancellationToken ct)
    {
        var p = await _db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubmittedById == _actor.Id, ct)
            ?? throw AppException.NotFound("You haven't submitted an application yet.");
        return p.ToDto();
    }

    /// The login gate polls this — one row, cheap.
    public async Task<ProviderStatusDto> GetMyStatusAsync(CancellationToken ct)
    {
        var row = await _db.Providers.AsNoTracking()
            .Where(x => x.SubmittedById == _actor.Id)
            .Select(x => new { x.Status, x.ReviewNote })
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("You haven't submitted an application yet.");
        return new ProviderStatusDto(_actor.Id, row.Status.ToWire(), row.ReviewNote);
    }

    public async Task<ProviderDto> GetByIdAsync(Guid providerId, CancellationToken ct)
    {
        var p = await _db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == providerId, ct)
            ?? throw AppException.NotFound("Provider not found.");
        return p.ToDto();
    }

    /// The Local Services list: approved providers, newest first. Category and
    /// search filtering happens here in SQL (the app previously did it
    /// client-side over a Firestore stream).
    public async Task<IReadOnlyList<ProviderDto>> ListApprovedAsync(string? category, string? search, CancellationToken ct)
    {
        var q = _db.Providers.AsNoTracking().Where(p => p.Status == ProviderStatus.Approved);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(p => p.ServiceCategories.Contains(category));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            q = q.Where(p =>
                EF.Functions.ILike(p.FullName, term) ||
                EF.Functions.ILike(p.Profession, term) ||
                EF.Functions.ILike(p.ServiceArea, term));
        }

        var rows = await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return rows.Select(p => p.ToDto()).ToList();
    }

    // ── Reviews ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ReviewDto>> GetReviewsAsync(Guid providerId, CancellationToken ct)
    {
        var exists = await _db.Providers.AnyAsync(p => p.Id == providerId, ct);
        if (!exists) throw AppException.NotFound("Provider not found.");

        var rows = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProviderId == providerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => r.ToDto(_actor.IdOrNull)).ToList();
    }

    /// Add or update the caller's review, then recompute the provider's rating
    /// rollup in the same transaction so average/count never drift.
    public async Task<ReviewDto> UpsertReviewAsync(Guid providerId, UpsertReviewRequest req, CancellationToken ct)
    {
        var userId = _actor.Id;

        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, ct)
            ?? throw AppException.NotFound("Provider not found.");

        if (provider.SubmittedById == userId)
            throw AppException.Forbidden("You can't review your own profile.");

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.ProviderId == providerId && r.UserId == userId, ct);

        if (review is null)
        {
            review = new ProviderReviewEntity
            {
                ProviderId = providerId,
                UserId = userId,
                Author = string.IsNullOrWhiteSpace(_actor.Name) ? "Customer" : _actor.Name,
                Rating = req.Rating,
                Comment = req.Comment.Trim(),
            };
            _db.Reviews.Add(review);
        }
        else
        {
            review.Rating = req.Rating;
            review.Comment = req.Comment.Trim();
            review.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await RecomputeRatingAsync(providerId, ct);

        return review.ToDto(userId);
    }

    public async Task DeleteReviewAsync(Guid providerId, CancellationToken ct)
    {
        var userId = _actor.Id;
        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.ProviderId == providerId && r.UserId == userId, ct)
            ?? throw AppException.NotFound("You haven't reviewed this provider.");

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
        await RecomputeRatingAsync(providerId, ct);
    }

    private async Task RecomputeRatingAsync(Guid providerId, CancellationToken ct)
    {
        var stats = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProviderId == providerId)
            .GroupBy(r => 1)
            .Select(g => new { Avg = g.Average(x => x.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, ct);
        if (provider is null) return;

        provider.RatingAverage = stats?.Avg ?? 0;
        provider.RatingCount = stats?.Count ?? 0;
        await _db.SaveChangesAsync(ct);
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProviderDto>> AdminListAsync(string? status, CancellationToken ct)
    {
        RequireAdmin();
        var q = _db.Providers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = EnumMapping.ToStatus(status);
            q = q.Where(p => p.Status == parsed);
        }

        var rows = await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return rows.Select(p => p.ToDto()).ToList();
    }

    public async Task<ProviderDto> ReviewApplicationAsync(Guid providerId, ReviewProviderRequest req, CancellationToken ct)
    {
        RequireAdmin();

        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, ct)
            ?? throw AppException.NotFound("Provider not found.");

        if (req.Approve)
        {
            provider.Status = ProviderStatus.Approved;
            provider.ReviewNote = string.Empty;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(req.ReviewNote))
                throw new AppException("A note is required when rejecting an application.");
            provider.Status = ProviderStatus.Rejected;
            provider.ReviewNote = req.ReviewNote.Trim();
        }

        provider.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return provider.ToDto();
    }

    private void RequireAdmin()
    {
        if (!_actor.IsAdmin) throw AppException.Forbidden("Admin access required.");
    }
}