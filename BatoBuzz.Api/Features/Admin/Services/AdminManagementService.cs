using BatoBuzz.Admin.Dtos;
using BatoBuzz.Admin.Services;
using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Identity.Data;
using BatoBuzz.Merchant.Data;
using BatoBuzz.Merchant.Enums;
using BatoBuzz.Awards.Data;
using BatoBuzz.Awards.Enums;
using BatoBuzz.Points.Data;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Admin.Services;

public interface IAdminManagementService
{
    Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct);

    Task<AdminUserListPage> ListUsersAsync(string? search, bool includeDeleted, string? cursor, int pageSize, CancellationToken ct);
    Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken ct);
    Task<AdminUserDto> SetUserSuspendedAsync(Guid id, bool suspend, string? note, CancellationToken ct);
    Task DeleteUserAsync(Guid id, CancellationToken ct);

    Task<AdminMerchantListPage> ListMerchantsAsync(string? search, string? status, bool includeDeleted, string? cursor, int pageSize, CancellationToken ct);
    Task<AdminMerchantDto> GetMerchantAsync(Guid id, CancellationToken ct);
    Task<AdminMerchantDto> ReviewMerchantAsync(Guid id, bool approve, string? note, CancellationToken ct);
    Task<AdminMerchantDto> SetMerchantSuspendedAsync(Guid id, bool suspend, string? note, CancellationToken ct);
    Task DeleteMerchantAsync(Guid id, CancellationToken ct);
}

/// Read + manage users and merchants, and the at-a-glance dashboard. Reads span
/// several feature databases (Identity, Points, Feed, Merchant, Awards); each is
/// its own DbContext, so there are no cross-database joins — counts and lookups
/// are separate queries, which is fine at admin volumes.
public sealed class AdminManagementService : IAdminManagementService
{
    private readonly IdentityDbContext _identity;
    private readonly MerchantDbContext _merchant;
    private readonly FeedDbContext _feed;
    private readonly PointsDbContext _points;
    private readonly AwardsDbContext _awards;
    private readonly IAdminAudit _audit;

    public AdminManagementService(
        IdentityDbContext identity, MerchantDbContext merchant, FeedDbContext feed,
        PointsDbContext points, AwardsDbContext awards, IAdminAudit audit)
    {
        _identity = identity; _merchant = merchant; _feed = feed;
        _points = points; _awards = awards; _audit = audit;
    }

    public async Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct)
    {
        var totalUsers = await _identity.Users.CountAsync(u => !u.IsDeleted, ct);
        var suspendedUsers = await _identity.Users.CountAsync(u => u.IsSuspended && !u.IsDeleted, ct);

        var totalMerchants = await _merchant.Merchants.CountAsync(m => !m.IsDeleted, ct);
        var pendingMerchants = await _merchant.Merchants.CountAsync(m => m.Status == MerchantStatus.Pending && !m.IsDeleted, ct);
        var approvedMerchants = await _merchant.Merchants.CountAsync(m => m.Status == MerchantStatus.Approved && !m.IsDeleted, ct);

        var totalPosts = await _feed.Posts.CountAsync(p => !p.IsDeleted && p.PostType == PostType.Ads, ct);
        var totalReels = await _feed.Posts.CountAsync(p => !p.IsDeleted && p.PostType == PostType.Reels, ct);
        var totalJobs = await _feed.Posts.CountAsync(p => !p.IsDeleted && p.PostType == PostType.Job, ct);
        var totalComments = await _feed.PostComments.CountAsync(ct);
        var openReports = await _feed.PostReports.CountAsync(ct);

        var activeParticipants = await _awards.Participants
            .CountAsync(p => p.Status == ParticipationStatus.Approved, ct);

        return new DashboardStatsDto(
            totalUsers, suspendedUsers,
            totalMerchants, pendingMerchants, approvedMerchants,
            totalPosts, totalReels, totalJobs,
            totalComments, openReports, activeParticipants);
    }

    // ── Users ────────────────────────────────────────────────────────────────

    public async Task<AdminUserListPage> ListUsersAsync(string? search, bool includeDeleted, string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize is < 1 or > 100 ? 30 : pageSize;
        var q = _identity.Users.AsNoTracking().AsQueryable();
        if (!includeDeleted) q = q.Where(u => !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u => u.DisplayName.ToLower().Contains(s)
                          || u.Email.ToLower().Contains(s)
                          || (u.Phone != null && u.Phone.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out var before))
            q = q.Where(u => u.CreatedAt < before);

        var rows = await q.OrderByDescending(u => u.CreatedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1].CreatedAt) : null;

        // points in one batched lookup
        var ids = rows.Select(u => u.Id).ToList();
        var points = await _points.UserPoints.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.TotalPoints, ct);

        var items = rows.Select(u => new AdminUserDto(
            u.Id, u.DisplayName, u.Email, u.Phone, u.PhotoUrl,
            u.IsSuspended, u.IsDeleted, u.AdminNote,
            points.TryGetValue(u.Id, out var pts) ? pts : 0, u.CreatedAt)).ToList();

        return new AdminUserListPage(items, next, hasMore);
    }

    public async Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken ct)
    {
        var u = await _identity.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("User not found.");
        var pts = await _points.UserPoints.AsNoTracking()
            .Where(p => p.UserId == id).Select(p => (int?)p.TotalPoints).FirstOrDefaultAsync(ct) ?? 0;
        return new AdminUserDto(u.Id, u.DisplayName, u.Email, u.Phone, u.PhotoUrl,
            u.IsSuspended, u.IsDeleted, u.AdminNote, pts, u.CreatedAt);
    }

    public async Task<AdminUserDto> SetUserSuspendedAsync(Guid id, bool suspend, string? note, CancellationToken ct)
    {
        var u = await _identity.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("User not found.");
        u.IsSuspended = suspend;
        if (note is not null) u.AdminNote = note;
        await _identity.SaveChangesAsync(ct);
        await _audit.LogAsync(suspend ? "user.suspend" : "user.unsuspend", "user", id.ToString(), note, ct);
        return await GetUserAsync(id, ct);
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken ct)
    {
        var u = await _identity.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("User not found.");
        u.IsDeleted = true;
        u.IsSuspended = true;
        await _identity.SaveChangesAsync(ct);
        await _audit.LogAsync("user.delete", "user", id.ToString(), null, ct);
    }

    // ── Merchants ──────────────────────────────────────────────────────────────

    public async Task<AdminMerchantListPage> ListMerchantsAsync(string? search, string? status, bool includeDeleted, string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize is < 1 or > 100 ? 30 : pageSize;
        var q = _merchant.Merchants.AsNoTracking().AsQueryable();
        if (!includeDeleted) q = q.Where(m => !m.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MerchantStatus>(status, true, out var st))
            q = q.Where(m => m.Status == st);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(m => m.BusinessName.ToLower().Contains(s) || m.Phone.Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out var before))
            q = q.Where(m => m.CreatedAt < before);

        var rows = await q.OrderByDescending(m => m.CreatedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1].CreatedAt) : null;

        var items = rows.Select(ToDto).ToList();
        return new AdminMerchantListPage(items, next, hasMore);
    }

    public async Task<AdminMerchantDto> GetMerchantAsync(Guid id, CancellationToken ct)
    {
        var m = await _merchant.Merchants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("Merchant not found.");
        return ToDto(m);
    }

    public async Task<AdminMerchantDto> ReviewMerchantAsync(Guid id, bool approve, string? note, CancellationToken ct)
    {
        var m = await _merchant.Merchants.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("Merchant not found.");
        m.Status = approve ? MerchantStatus.Approved : MerchantStatus.Rejected;
        if (note is not null) m.AdminNote = note;
        await _merchant.SaveChangesAsync(ct);
        await _audit.LogAsync(approve ? "merchant.approve" : "merchant.reject", "merchant", id.ToString(), note, ct);
        return ToDto(m);
    }

    public async Task<AdminMerchantDto> SetMerchantSuspendedAsync(Guid id, bool suspend, string? note, CancellationToken ct)
    {
        var m = await _merchant.Merchants.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("Merchant not found.");
        m.IsSuspended = suspend;
        if (note is not null) m.AdminNote = note;
        await _merchant.SaveChangesAsync(ct);
        await _audit.LogAsync(suspend ? "merchant.suspend" : "merchant.unsuspend", "merchant", id.ToString(), note, ct);
        return ToDto(m);
    }

    public async Task DeleteMerchantAsync(Guid id, CancellationToken ct)
    {
        var m = await _merchant.Merchants.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("Merchant not found.");
        m.IsDeleted = true;
        m.IsSuspended = true;
        await _merchant.SaveChangesAsync(ct);
        await _audit.LogAsync("merchant.delete", "merchant", id.ToString(), null, ct);
    }

    private static AdminMerchantDto ToDto(BatoBuzz.Merchant.Entities.MerchantProfile m) => new(
        m.Id, m.BusinessName, m.Phone, m.OwnerPhotoUrl,
        m.Status.ToString().ToLowerInvariant(), m.IsSuspended, m.IsDeleted, m.AdminNote, m.CreatedAt);

    private static string EncodeCursor(DateTime t)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(t.ToString("O")));

    private static bool TryDecodeCursor(string cursor, out DateTime before)
    {
        before = default;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out before);
        }
        catch { return false; }
    }
}