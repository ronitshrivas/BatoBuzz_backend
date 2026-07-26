using BatoBuzz.Points.Enums;

namespace BatoBuzz.Points.Entities;

/// One points event — the app's `userPoints/{uid}/history` subcollection.
/// Drives the history list and the "points earned today" figure.
public class PointHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public PointAction Action { get; set; }
    public int Points { get; set; }                 // negative when revoked

    public string TargetId { get; set; } = string.Empty;
    public Guid? PostId { get; set; }
    public Guid? MerchantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}