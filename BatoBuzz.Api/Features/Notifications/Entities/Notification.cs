using BatoBuzz.Notifications.Enums;

namespace BatoBuzz.Notifications.Entities;

/// A notification delivered to one recipient (a user or a merchant — the
/// RecipientId is whichever account should see it). Mirrors the app's
/// NotificationModel: who acted, what kind, a title/body, optional deep-link
/// ids, read state.
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipientId { get; set; }

    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? ActorPhoto { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = "BatoBuzz";
    public string Body { get; set; } = string.Empty;

    public Guid? PostId { get; set; }
    public Guid? ThreadId { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}