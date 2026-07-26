using BatoBuzz.Points.Enums;

namespace BatoBuzz.Points.Entities;

/// The idempotency latch: one row per (user, action, target), mirroring the
/// app's `userPointsActions/{uid}_{action}_{targetId}` document.
///
/// This is what stops points farming — liking, unliking and re-liking the same
/// post can only ever earn its points once. A unique index enforces it at the
/// database level, so concurrent requests can't both slip through.
public class PointLatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public PointAction Action { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}