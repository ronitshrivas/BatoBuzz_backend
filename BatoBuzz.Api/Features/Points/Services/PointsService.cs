
using BatoBuzz.Points.Data;
using BatoBuzz.Points.Dtos;
using BatoBuzz.Points.Entities;
using BatoBuzz.Points.Enums;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Points.Services;

public interface IPointsService
{
    Task<UserPointsDto> GetMyPointsAsync(CancellationToken ct);
    Task<int> GetPointsTodayAsync(CancellationToken ct);
    Task<PointHistoryPage> GetHistoryAsync(string? cursor, int pageSize, CancellationToken ct);
    Task<PointActionResult> AwardAsync(PointActionRequest req, CancellationToken ct);
    Task<PointActionResult> RevokeAsync(PointActionRequest req, CancellationToken ct);
    Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(int limit, CancellationToken ct);
    Task<MyStandingDto> GetMyStandingAsync(CancellationToken ct);
}

public sealed class PointsService : IPointsService
{
    private readonly PointsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IUserDirectory _users;

    public PointsService(PointsDbContext db, ICurrentUser user, IUserDirectory users)
        => (_db, _user, _users) = (db, user, users);

    public async Task<UserPointsDto> GetMyPointsAsync(CancellationToken ct)
    {
        var row = await _db.UserPoints.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == _user.Id, ct);

        // A user with no points yet isn't an error — they're simply at zero.
        return row is null
            ? new UserPointsDto(_user.Id, 0, DateTime.UtcNow, Array.Empty<AchievementDto>())
            : ToDto(row);
    }

    /// Sum of today's history entries (UTC day), for the "earned today" figure.
    public async Task<int> GetPointsTodayAsync(CancellationToken ct)
    {
        var midnight = DateTime.UtcNow.Date;
        return await _db.History.AsNoTracking()
            .Where(h => h.UserId == _user.Id && h.CreatedAt >= midnight)
            .SumAsync(h => h.Points, ct);
    }

    /// Newest-first history, keyset-paginated on CreatedAt so paging stays
    /// correct as new entries arrive (offset paging would duplicate rows).
    public async Task<PointHistoryPage> GetHistoryAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = _db.History.AsNoTracking().Where(h => h.UserId == _user.Id);

        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out var before))
            q = q.Where(h => h.CreatedAt < before);

        var rows = await q.OrderByDescending(h => h.CreatedAt)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1].CreatedAt) : null;

        return new PointHistoryPage(
            rows.Select(h => new PointHistoryDto(
                h.Id, h.Action.ToWire(), h.Points, h.TargetId, h.PostId, h.MerchantId, h.CreatedAt)).ToList(),
            next, hasMore);
    }

    /// Grants points once per (user, action, target).
    ///
    /// The latch row is inserted in the same transaction as the points update,
    /// and a unique index backs it — so two concurrent likes can't both award.
    /// If the latch already exists, this is a no-op that reports Awarded=false
    /// rather than an error, because the caller (a like button) shouldn't fail
    /// just because points were already counted.
    public async Task<PointActionResult> AwardAsync(PointActionRequest req, CancellationToken ct)
    {
        var action = PointValues.Parse(req.Action)
            ?? throw new AppException("Unknown action. Expected like, comment, share, or qr_scan.");
        var pts = PointValues.ForAction(action);
        if (pts == 0) throw new AppException("That action doesn't earn points.");

        var userId = _user.Id;
        var targetId = req.TargetId.Trim();
        if (targetId.Length == 0) throw new AppException("A target is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var already = await _db.Latches
            .AnyAsync(l => l.UserId == userId && l.Action == action && l.TargetId == targetId, ct);
        if (already)
        {
            var current = await GetTotalAsync(userId, ct);
            await tx.CommitAsync(ct);
            return new PointActionResult(false, 0, current);
        }

        _db.Latches.Add(new PointLatch { UserId = userId, Action = action, TargetId = targetId });

        var total = await ApplyDeltaAsync(userId, pts, ct);

        _db.History.Add(new PointHistoryEntry
        {
            UserId = userId,
            Action = action,
            Points = pts,
            TargetId = targetId,
            PostId = req.PostId,
            MerchantId = req.MerchantId,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the unique latch index — the other request awarded.
            await tx.RollbackAsync(ct);
            return new PointActionResult(false, 0, await GetTotalAsync(userId, ct));
        }

        return new PointActionResult(true, pts, total);
    }

    /// Reverses a previously awarded action (e.g. un-liking). Removes the latch
    /// so the action could legitimately earn again later, subtracts the points,
    /// and records a negative history entry for an auditable trail.
    public async Task<PointActionResult> RevokeAsync(PointActionRequest req, CancellationToken ct)
    {
        var action = PointValues.Parse(req.Action)
            ?? throw new AppException("Unknown action. Expected like, comment, share, or qr_scan.");
        var pts = PointValues.ForAction(action);

        var userId = _user.Id;
        var targetId = req.TargetId.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var latch = await _db.Latches
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Action == action && l.TargetId == targetId, ct);

        if (latch is null)
        {
            var current = await GetTotalAsync(userId, ct);
            await tx.CommitAsync(ct);
            return new PointActionResult(false, 0, current);
        }

        _db.Latches.Remove(latch);
        var total = await ApplyDeltaAsync(userId, -pts, ct);

        _db.History.Add(new PointHistoryEntry
        {
            UserId = userId,
            Action = action,
            Points = -pts,
            TargetId = targetId,
            PostId = req.PostId,
            MerchantId = req.MerchantId,
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PointActionResult(true, -pts, total);
    }

    /// Top users by points. Ties break toward the *earlier* UpdatedAt, matching
    /// the app's rule that whoever reached the score first ranks higher.
    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(int limit, CancellationToken ct)
    {
        limit = limit is < 1 or > 100 ? 10 : limit;

        var rows = await _db.UserPoints.AsNoTracking()
            .OrderByDescending(p => p.TotalPoints)
            .ThenBy(p => p.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var profiles = await _users.GetProfilesAsync(rows.Select(r => r.UserId), ct);

        return rows.Select((r, i) =>
        {
            profiles.TryGetValue(r.UserId, out var p);
            return new LeaderboardEntryDto(
                r.UserId,
                string.IsNullOrWhiteSpace(p.DisplayName) ? "BatoBuzz User" : p.DisplayName,
                p.PhotoUrl,
                r.TotalPoints,
                i + 1);
        }).ToList();
    }

    /// The caller's rank without scanning the whole table: count how many rank
    /// above them under the same tie rule, then add one.
    public async Task<MyStandingDto> GetMyStandingAsync(CancellationToken ct)
    {
        var me = await _db.UserPoints.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == _user.Id, ct);

        var myPoints = me?.TotalPoints ?? 0;
        var myUpdated = me?.UpdatedAt ?? DateTime.UtcNow;

        var ahead = await _db.UserPoints.AsNoTracking()
            .CountAsync(p => p.UserId != _user.Id &&
                             (p.TotalPoints > myPoints ||
                              (p.TotalPoints == myPoints && p.UpdatedAt < myUpdated)), ct);

        var leader = await _db.UserPoints.AsNoTracking()
            .OrderByDescending(p => p.TotalPoints)
            .Select(p => (int?)p.TotalPoints)
            .FirstOrDefaultAsync(ct) ?? myPoints;

        return new MyStandingDto(ahead + 1, Math.Max(leader, myPoints), myPoints);
    }

    // ── internals ────────────────────────────────────────────────────────────

    private async Task<int> GetTotalAsync(Guid userId, CancellationToken ct)
        => await _db.UserPoints.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.TotalPoints)
            .FirstOrDefaultAsync(ct) ?? 0;

    /// Adds (or subtracts) points, creating the row on first award. Total is
    /// floored at zero so a revoke can never push someone negative.
    private async Task<int> ApplyDeltaAsync(Guid userId, int delta, CancellationToken ct)
    {
        var row = await _db.UserPoints.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            row = new UserPoints { UserId = userId, TotalPoints = 0 };
            _db.UserPoints.Add(row);
        }

        row.TotalPoints = Math.Max(0, row.TotalPoints + delta);
        row.UpdatedAt = DateTime.UtcNow;
        return row.TotalPoints;
    }

    private static UserPointsDto ToDto(UserPoints p) => new(
        p.UserId, p.TotalPoints, p.UpdatedAt,
        p.Achievements.Select(a => new AchievementDto(a.Tier, a.Label, a.Season)).ToList());

    private static string EncodeCursor(DateTime createdAt)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(createdAt.ToString("O")));

    private static bool TryDecodeCursor(string cursor, out DateTime before)
    {
        before = default;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTime.TryParse(raw, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out before);
        }
        catch { return false; }
    }
}