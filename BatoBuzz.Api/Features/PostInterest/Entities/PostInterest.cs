namespace BatoBuzz.PostInterest.Entities;

/// A user expressing interest in an event post. Mirrors the app's `PostInterests`
/// collection (doc id `{postId}_{userId}`), keeping the same denormalised
/// snapshots so the merchant's "interested users" list renders without extra
/// reads. One interest per (post, user), enforced by a unique index.
public class PostInterest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public Guid MerchantId { get; set; }

    /// "job" | "event" — matches the app's postType.
    public string PostType { get; set; } = "event";

    // User snapshot (so the merchant can contact them).
    public string UserName { get; set; } = string.Empty;
    public string UserPhone { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserPhoto { get; set; } = string.Empty;

    // Post snapshot.
    public string PostTitle { get; set; } = string.Empty;
    public string PostLocation { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantPhoto { get; set; } = string.Empty;

    public DateTime InterestedAt { get; set; } = DateTime.UtcNow;
}
