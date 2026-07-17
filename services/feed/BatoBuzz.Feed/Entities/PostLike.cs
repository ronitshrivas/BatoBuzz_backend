using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Entities;

/// One row per (post, actor). The unique index on (PostId, ActorId) is what
/// makes liking idempotent — it replaces Firestore's `likedBy` array.
public class PostLike
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }
    public Post? Post { get; set; }

    /// The liker — a user id or a merchant id depending on which app acted.
    public Guid ActorId { get; set; }
    public AuthorType ActorType { get; set; } = AuthorType.User;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
