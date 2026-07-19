using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Entities;

/// A comment or a reply. Replies are self-referencing rows with ParentId set,
/// matching the comments/{id}/replies subcollection the apps used in Firestore
/// while keeping a single table.
public class PostComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }
    public Post? Post { get; set; }

    /// Null for a top-level comment; set to the parent comment for a reply.
    public Guid? ParentId { get; set; }
    public PostComment? Parent { get; set; }
    public List<PostComment> Replies { get; set; } = new();

    // ── Author (denormalized; either app can comment)
    public Guid AuthorId { get; set; }
    public AuthorType AuthorType { get; set; } = AuthorType.User;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorPhoto { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// Denormalized count of direct replies.
    public int ReplyCount { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
