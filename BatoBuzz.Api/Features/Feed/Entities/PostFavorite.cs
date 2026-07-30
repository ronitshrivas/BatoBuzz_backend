namespace BatoBuzz.Feed.Entities;

/// One row per (post, user) a user has saved. The unique index on
/// (UserId, PostId) makes toggling idempotent and mirrors the app's
/// per-user `favorites/{postId}` subcollection (doc id = post id, savedAt).
public class PostFavorite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }
    public Post? Post { get; set; }

    public Guid UserId { get; set; }

    /// When it was saved — drives "my favorites, most recently saved first".
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}