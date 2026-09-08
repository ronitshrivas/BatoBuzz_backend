namespace BatoBuzz.Reservations.Entities;

/// A merchant's block on a specific user from placing further holds. Mirrors the
/// app's `merchants/{merchantId}/blacklist/{userId}` document: the user may read
/// only their own entry, and an entry counts only while IsActive is true.
public class MerchantBlacklistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MerchantId { get; set; }
    public Guid UserId { get; set; }

    public bool IsActive { get; set; } = true;
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
