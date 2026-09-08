namespace BatoBuzz.Follow.Entities;

/// A user following a merchant. Replaces the app's two mirrored subcollections
/// (`users/{u}/following/{m}` and `merchantsRegistration/{m}/followers/{u}`) plus
/// the denormalised `followerCount` with a single row per (user, merchant); the
/// count is a cheap COUNT over this table, so nothing can drift out of sync.
public class Follow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid MerchantId { get; set; }

    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
}
