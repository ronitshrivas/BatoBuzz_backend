using BatoBuzz.Reservations.Enums;

namespace BatoBuzz.Reservations.Entities;

/// A grab / reserve hold a user places on a merchant post. Mirrors the fields
/// the Flutter `ReservationModel` reads from the old Firestore `reservations`
/// collection, so the apps' models need no rewrite when they switch to the API.
///
/// Server-authoritative rules (enforced in ReservationService):
///   • an 8-hour hold from creation (ExpiresAt),
///   • the user may cancel only within the first hour (CreatedAt + 1h),
///   • at most 3 ACTIVE holds per user,
///   • one ACTIVE hold per (user, post),
///   • stock is held on grab and only permanently reduced on completion.
public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid PostId { get; set; }

    // Snapshots taken at grab time so a list renders without extra reads. Names
    // match the app's `productTitleSnapshot` / `productImageSnapshot` etc.
    public string ProductTitle { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public decimal ReservedPrice { get; set; }
    public int Quantity { get; set; } = 1;

    public ReservationStatus Status { get; set; } = ReservationStatus.ACTIVE;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// Set true once completion has awarded points, so a repeat completion is
    /// idempotent and never double-awards.
    public bool PointsAwarded { get; set; }

    // Actor snapshots for list rendering on both sides.
    public string UserName { get; set; } = string.Empty;
    public string UserPhoto { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantPhoto { get; set; } = string.Empty;
}
