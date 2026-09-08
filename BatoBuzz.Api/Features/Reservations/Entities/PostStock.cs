namespace BatoBuzz.Reservations.Entities;

/// The reservable-stock ledger for a post, owned by the Reservations feature so
/// it never reaches across into the Feed database. `StockAvailable` is the
/// number of units a customer can still grab right now; a grab decrements it
/// (the units are held), a cancel/expiry returns them, and a completion makes
/// the reduction permanent (the units were actually handed over).
///
/// A row is created lazily the first time a post is grabbed, seeded from the
/// initial stock the caller passes (the app knows the post's stock). Rows use
/// PostId as the primary key: one ledger row per post.
public class PostStock
{
    public Guid PostId { get; set; }

    public int StockAvailable { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
