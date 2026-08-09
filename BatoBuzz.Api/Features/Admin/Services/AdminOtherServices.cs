using BatoBuzz.Admin.Dtos;
using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Identity.Data;
using BatoBuzz.Notifications.Data;
using BatoBuzz.Notifications.Entities;
using BatoBuzz.Notifications.Enums;
using BatoBuzz.Points.Data;
using BatoBuzz.Points.Entities;
using BatoBuzz.Points.Enums;
using BatoBuzz.Admin.Data;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;


namespace BatoBuzz.Admin.Services;

// ── Content moderation ─────────────────────────────────────────────────────────

public interface IAdminModerationService
{
    Task<AdminListPage<AdminPostDto>> ListPostsAsync(string? type, bool includeDeleted, string? cursor, int pageSize, CancellationToken ct);
    Task RemovePostAsync(Guid postId, string? reason, CancellationToken ct);
    Task RestorePostAsync(Guid postId, CancellationToken ct);
    Task RemoveCommentAsync(Guid commentId, CancellationToken ct);
    Task<AdminListPage<AdminReportDto>> ListReportsAsync(string? cursor, int pageSize, CancellationToken ct);
    Task ResolveReportAsync(Guid reportId, CancellationToken ct);
    Task RemoveRatingAsync(Guid ratingId, CancellationToken ct);
}

public sealed class AdminModerationService : IAdminModerationService
{
    private readonly FeedDbContext _feed;
    private readonly BatoBuzz.Merchant.Data.MerchantDbContext _merchant;
    private readonly IAdminAudit _audit;

    public AdminModerationService(FeedDbContext feed, BatoBuzz.Merchant.Data.MerchantDbContext merchant, IAdminAudit audit)
        => (_feed, _merchant, _audit) = (feed, merchant, audit);

    public async Task<AdminListPage<AdminPostDto>> ListPostsAsync(string? type, bool includeDeleted, string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize is < 1 or > 100 ? 30 : pageSize;
        var q = _feed.Posts.AsNoTracking().AsQueryable();
        if (!includeDeleted) q = q.Where(p => !p.IsDeleted);
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<PostType>(type, true, out var pt))
            q = q.Where(p => p.PostType == pt);
        if (!string.IsNullOrWhiteSpace(cursor) && Cursor.TryDecode(cursor, out var before))
            q = q.Where(p => p.CreatedAt < before);

        var rows = await q.OrderByDescending(p => p.CreatedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? Cursor.Encode(rows[^1].CreatedAt) : null;

        var items = rows.Select(p => new AdminPostDto(
            p.Id, p.MerchantId, p.Body, p.PostType.ToString().ToLowerInvariant(), p.IsDeleted, p.CreatedAt)).ToList();
        return new AdminListPage<AdminPostDto>(items, next, hasMore);
    }

    public async Task RemovePostAsync(Guid postId, string? reason, CancellationToken ct)
    {
        var p = await _feed.Posts.FirstOrDefaultAsync(x => x.Id == postId, ct)
            ?? throw AppException.NotFound("Post not found.");
        p.IsDeleted = true;
        await _feed.SaveChangesAsync(ct);
        await _audit.LogAsync("post.remove", "post", postId.ToString(), reason, ct);
    }

    public async Task RestorePostAsync(Guid postId, CancellationToken ct)
    {
        var p = await _feed.Posts.FirstOrDefaultAsync(x => x.Id == postId, ct)
            ?? throw AppException.NotFound("Post not found.");
        p.IsDeleted = false;
        await _feed.SaveChangesAsync(ct);
        await _audit.LogAsync("post.restore", "post", postId.ToString(), null, ct);
    }

    public async Task RemoveCommentAsync(Guid commentId, CancellationToken ct)
    {
        var deleted = await _feed.PostComments.Where(c => c.Id == commentId).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw AppException.NotFound("Comment not found.");
        await _audit.LogAsync("comment.remove", "comment", commentId.ToString(), null, ct);
    }

    public async Task<AdminListPage<AdminReportDto>> ListReportsAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize is < 1 or > 100 ? 30 : pageSize;
        var q = _feed.PostReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(cursor) && Cursor.TryDecode(cursor, out var before))
            q = q.Where(r => r.CreatedAt < before);

        var rows = await q.OrderByDescending(r => r.CreatedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? Cursor.Encode(rows[^1].CreatedAt) : null;

        var items = rows.Select(r => new AdminReportDto(r.Id, r.PostId, r.ReporterId, r.Reason, r.CreatedAt)).ToList();
        return new AdminListPage<AdminReportDto>(items, next, hasMore);
    }

    public async Task ResolveReportAsync(Guid reportId, CancellationToken ct)
    {
        var deleted = await _feed.PostReports.Where(r => r.Id == reportId).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw AppException.NotFound("Report not found.");
        await _audit.LogAsync("report.resolve", "report", reportId.ToString(), null, ct);
    }

    public async Task RemoveRatingAsync(Guid ratingId, CancellationToken ct)
    {
        var deleted = await _merchant.MerchantRatings.Where(r => r.Id == ratingId).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw AppException.NotFound("Rating not found.");
        await _audit.LogAsync("rating.remove", "rating", ratingId.ToString(), null, ct);
    }
}

// ── Points control ─────────────────────────────────────────────────────────────

public interface IAdminPointsService
{
    Task<AdminPointsDto> GetAsync(Guid userId, CancellationToken ct);
    Task<AdminPointsDto> AdjustAsync(Guid userId, int delta, string reason, CancellationToken ct);
}

public sealed class AdminPointsService : IAdminPointsService
{
    private readonly PointsDbContext _points;
    private readonly IAdminAudit _audit;

    public AdminPointsService(PointsDbContext points, IAdminAudit audit)
        => (_points, _audit) = (points, audit);

    public async Task<AdminPointsDto> GetAsync(Guid userId, CancellationToken ct)
    {
        var row = await _points.UserPoints.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        return new AdminPointsDto(userId, row?.TotalPoints ?? 0, row?.UpdatedAt ?? DateTime.UtcNow);
    }

    /// Add or subtract points from a user (for correcting disputes or abuse).
    /// Recorded in points history and the admin audit log.
    public async Task<AdminPointsDto> AdjustAsync(Guid userId, int delta, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new AppException("A reason is required for a manual points adjustment.");

        var row = await _points.UserPoints.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            row = new UserPoints { UserId = userId, TotalPoints = 0 };
            _points.UserPoints.Add(row);
        }
        row.TotalPoints = Math.Max(0, row.TotalPoints + delta);
        row.UpdatedAt = DateTime.UtcNow;

        _points.History.Add(new PointHistoryEntry
        {
            UserId = userId,
            Action = PointAction.QrScan,   // closest existing action; detail carries the real reason
            Points = delta,
            TargetId = "admin-adjust",
        });

        await _points.SaveChangesAsync(ct);
        await _audit.LogAsync("points.adjust", "user", userId.ToString(), $"delta={delta}; {reason}", ct);
        return new AdminPointsDto(userId, row.TotalPoints, row.UpdatedAt);
    }
}

// ── Broadcast notifications ──────────────────────────────────────────────────────

public interface IAdminBroadcastService
{
    Task<BroadcastResult> BroadcastAsync(string audience, string title, string body, CancellationToken ct);
}

public sealed class AdminBroadcastService : IAdminBroadcastService
{
    private readonly NotificationsDbContext _notif;
    private readonly IdentityDbContext _identity;
    private readonly BatoBuzz.Merchant.Data.MerchantDbContext _merchant;
    private readonly IAdminAudit _audit;

    public AdminBroadcastService(
        NotificationsDbContext notif, IdentityDbContext identity,
        BatoBuzz.Merchant.Data.MerchantDbContext merchant, IAdminAudit audit)
    {
        _notif = notif; _identity = identity; _merchant = merchant; _audit = audit;
    }

    /// Fan a notification out to every user, every merchant, or both. Writes one
    /// Notification row per recipient (their in-app inbox). Push delivery is a
    /// separate concern; this populates what the app reads.
    public async Task<BroadcastResult> BroadcastAsync(string audience, string title, string body, CancellationToken ct)
    {
        audience = (audience ?? "").Trim().ToLowerInvariant();
        title = string.IsNullOrWhiteSpace(title) ? "BatoBuzz" : title.Trim();
        body = body.Trim();

        var recipientIds = new List<Guid>();
        if (audience is "users" or "all")
            recipientIds.AddRange(await _identity.Users.AsNoTracking()
                .Where(u => !u.IsDeleted).Select(u => u.Id).ToListAsync(ct));
        if (audience is "merchants" or "all")
            recipientIds.AddRange(await _merchant.Merchants.AsNoTracking()
                .Where(m => !m.IsDeleted).Select(m => m.Id).ToListAsync(ct));

        if (recipientIds.Count == 0) return new BroadcastResult(0);

        const int batch = 500;
        for (int i = 0; i < recipientIds.Count; i += batch)
        {
            foreach (var rid in recipientIds.Skip(i).Take(batch))
                _notif.Notifications.Add(new Notification
                {
                    RecipientId = rid,
                    ActorId = Guid.Empty,
                    ActorName = "BatoBuzz",
                    Type = NotificationType.Award,   // generic announcement bucket
                    Title = title,
                    Body = body,
                });
            await _notif.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("broadcast.send", "notification", audience, $"{recipientIds.Count} recipients: {title}", ct);
        return new BroadcastResult(recipientIds.Count);
    }
}

// ── Audit read ─────────────────────────────────────────────────────────────────

public interface IAdminAuditReader
{
    Task<AdminListPage<AuditEntryDto>> ListAsync(string? cursor, int pageSize, CancellationToken ct);
}

public sealed class AdminAuditReader : IAdminAuditReader
{
    private readonly AdminDbContext _db;
    public AdminAuditReader(AdminDbContext db) => _db = db;

    public async Task<AdminListPage<AuditEntryDto>> ListAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize is < 1 or > 100 ? 50 : pageSize;
        var q = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(cursor) && Cursor.TryDecode(cursor, out var before))
            q = q.Where(a => a.CreatedAt < before);

        var rows = await q.OrderByDescending(a => a.CreatedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? Cursor.Encode(rows[^1].CreatedAt) : null;

        var items = rows.Select(a => new AuditEntryDto(
            a.Id, a.ActorId, a.ActorName, a.Action, a.TargetType, a.TargetId, a.Detail, a.CreatedAt)).ToList();
        return new AdminListPage<AuditEntryDto>(items, next, hasMore);
    }
}

// ── shared cursor helper ─────────────────────────────────────────────────────────

internal static class Cursor
{
    public static string Encode(DateTime t)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(t.ToString("O")));

    public static bool TryDecode(string cursor, out DateTime before)
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