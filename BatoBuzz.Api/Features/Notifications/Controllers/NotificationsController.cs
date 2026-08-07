using BatoBuzz.Notifications.Dtos;
using BatoBuzz.Notifications.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Notifications.Controllers;

/// In-app notifications + FCM device-token registration. Everything acts as the
/// signed-in account (user or merchant).
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _svc;
    public NotificationsController(INotificationService svc) => _svc = svc;

    /// My notifications, newest first, paginated.
    [HttpGet]
    public async Task<IActionResult> Mine([FromQuery] string? cursor,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<NotificationsPage>.Ok(await _svc.GetMineAsync(cursor, pageSize, ct)));

    /// Unread count for the badge.
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(ApiResponse<UnreadCountDto>.Ok(await _svc.GetUnreadCountAsync(ct)));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _svc.MarkReadAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null, "Marked read."));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _svc.MarkAllReadAsync(ct);
        return Ok(ApiResponse<object>.Ok(null, "All marked read."));
    }

    /// Register/refresh this device's FCM token for push.
    [HttpPost("tokens")]
    public async Task<IActionResult> RegisterToken(RegisterTokenRequest req, CancellationToken ct)
    {
        await _svc.RegisterTokenAsync(req, ct);
        return Ok(ApiResponse<object>.Ok(null, "Token registered."));
    }

    /// Remove a device token (logout).
    [HttpDelete("tokens")]
    public async Task<IActionResult> UnregisterToken([FromQuery] string token, CancellationToken ct)
    {
        await _svc.UnregisterTokenAsync(token, ct);
        return Ok(ApiResponse<object>.Ok(null, "Token removed."));
    }

    /// Create a notification for another account (used by app-side triggers when
    /// someone likes, comments, messages, etc.).
    [HttpPost]
    public async Task<IActionResult> Create(CreateNotificationRequest req, CancellationToken ct)
        => Ok(ApiResponse<NotificationDto>.Ok(await _svc.CreateAsync(req, ct)));
}