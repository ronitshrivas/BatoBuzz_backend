using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Entities;

/// One row per (post, viewer) — replaces Firestore's `viewedBy` array and makes
/// view counting idempotent per viewer.
public class PostView
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }
    public Post? Post { get; set; }

    public Guid ViewerId { get; set; }
    public AuthorType ViewerType { get; set; } = AuthorType.User;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
