using System.Security.Claims;
using BatoBuzz.Notifications.Data;
using BatoBuzz.Notifications.Dtos;
using BatoBuzz.Notifications.Entities;
using BatoBuzz.Notifications.Enums;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Notifications.Services;

public interface INotificationService
{
    Task<NotificationsPage> GetMineAsync(string? cursor, int pageSize, CancellationToken ct);
    Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
    Task MarkAllReadAsync(CancellationToken ct);
    Task RegisterTokenAsync(RegisterTokenRequest req, CancellationToken ct);
    Task UnregisterTokenAsync(string token, CancellationToken ct);
    Task<NotificationDto> CreateAsync(CreateNotificationRequest req, CancellationToken ct);
}

/// Persisted notifications + device-token registry. Push *sending* (calling FCM)
/// is deliberately out of scope here — this stores the notification and the
/// tokens; a sender can be layered on later reading DeviceTokens. That keeps the
/// feature useful immediately (the in-app list works) without coupling it to an
/// FCM credential the server may not have yet.
public sealed class NotificationService : INotificationService
{
    private readonly NotificationsDbContext _db;
    private readonly IHttpContextAccessor _http;

    public NotificationService(NotificationsDbContext db, IHttpContextAccessor http)
        => (_db, _http) = (db, http);

    private Guid Me =>
        Guid.TryParse(_http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw AppException.Unauthorized("You must be signed in.");

    private string MyName =>
        _http.HttpContext?.User?.FindFirst("display_name")?.Value ?? "Someone";

    public async Task<NotificationsPage> GetMineAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var me = Me;
        pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;

        var q = _db.Notifications.AsNoTracking().Where(n => n.RecipientId == me);
        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out var before))
            q = q.Where(n => n.CreatedAt < before);

        var rows = await q.OrderByDescending(n => n.CreatedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1].CreatedAt) : null;
        return new NotificationsPage(rows.Select(ToDto).ToList(), next, hasMore);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct)
    {
        var me = Me;
        var count = await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientId == me && !n.IsRead, ct);
        return new UnreadCountDto(count);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var me = Me;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == me, ct)
            ?? throw AppException.NotFound("Notification not found.");
        if (!n.IsRead) { n.IsRead = true; await _db.SaveChangesAsync(ct); }
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var me = Me;
        await _db.Notifications
            .Where(n => n.RecipientId == me && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    /// Upsert a device token by its value. If the token was registered to another
    /// account (account switch on the same device), it moves to this one.
    public async Task RegisterTokenAsync(RegisterTokenRequest req, CancellationToken ct)
    {
        var me = Me;
        var token = req.Token.Trim();
        if (token.Length == 0) throw new AppException("A token is required.");

        var existing = await _db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
        if (existing is null)
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                OwnerId = me,
                Token = token,
                Platform = req.Platform,
            });
        }
        else
        {
            existing.OwnerId = me;
            existing.Platform = req.Platform ?? existing.Platform;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* concurrent upsert of same token — fine */ }
    }

    public async Task UnregisterTokenAsync(string token, CancellationToken ct)
    {
        var me = Me;
        token = (token ?? "").Trim();
        await _db.DeviceTokens
            .Where(t => t.Token == token && t.OwnerId == me)
            .ExecuteDeleteAsync(ct);
    }

    /// Create a notification for a recipient. The actor is the caller. Skips
    /// self-notifications (you don't get told about your own actions).
    public async Task<NotificationDto> CreateAsync(CreateNotificationRequest req, CancellationToken ct)
    {
        var actor = Me;
        if (req.RecipientId == actor)
            throw new AppException("Can't notify yourself.");

        var n = new Notification
        {
            RecipientId = req.RecipientId,
            ActorId = actor,
            ActorName = MyName,
            Type = NotificationTypeMap.Parse(req.Type),
            Title = string.IsNullOrWhiteSpace(req.Title) ? "BatoBuzz" : req.Title!.Trim(),
            Body = req.Body.Trim(),
            PostId = req.PostId,
            ThreadId = req.ThreadId,
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);
        return ToDto(n);
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id, n.ActorId, n.ActorName, n.ActorPhoto,
        n.Type.ToWire(), n.Title, n.Body, n.PostId, n.ThreadId, n.IsRead, n.CreatedAt);

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