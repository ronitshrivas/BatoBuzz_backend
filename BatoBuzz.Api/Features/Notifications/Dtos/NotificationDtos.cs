using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Notifications.Dtos;

public sealed record NotificationDto(
    Guid Id,
    Guid ActorId,
    string ActorName,
    string? ActorPhoto,
    string Type,
    string Title,
    string Body,
    Guid? PostId,
    Guid? ThreadId,
    bool IsRead,
    DateTime CreatedAt);

public sealed record NotificationsPage(
    IReadOnlyList<NotificationDto> Items,
    string? NextCursor,
    bool HasMore);

public sealed record UnreadCountDto(int Count);

/// Register (or refresh) an FCM token for the signed-in account.
public sealed record RegisterTokenRequest(
    [Required, MaxLength(500)] string Token,
    string? Platform);

/// Create a notification for a recipient. Internal-ish: used by the app's
/// notification_trigger to fan out events (someone liked/commented/etc.).
public sealed record CreateNotificationRequest(
    [Required] Guid RecipientId,
    [Required] string Type,
    string? Title,
    [Required] string Body,
    Guid? PostId,
    Guid? ThreadId);