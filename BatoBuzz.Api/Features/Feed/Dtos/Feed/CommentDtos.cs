using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Feed.Dtos.Feed;

/// Wire shape of a comment or reply.
///
/// The Flutter `PostCommentModel` reads `merchantId`/`merchantName`/
/// `merchantPhoto` and falls back to `userId`/`userName`/`userPhoto`. Both apps
/// can comment, so the author is exposed once under neutral names plus an
/// `authorType` ("user" | "merchant") — see the migration note in the README
/// for the two-line model change this needs.
public sealed record CommentDto(
    Guid CommentId,
    Guid PostId,
    Guid? ParentId,
    Guid AuthorId,
    string AuthorType,
    string AuthorName,
    string AuthorPhoto,
    string Text,
    int ReplyCount,
    bool CanEdit,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateCommentRequest(
    [Required, MaxLength(2000)] string Text,
    /// Set to reply to an existing comment; omit for a top-level comment.
    Guid? ParentId);

public sealed record UpdateCommentRequest(
    [Required, MaxLength(2000)] string Text);

public sealed record CommentQuery
{
    /// Omit for top-level comments; set to list replies under a comment.
    public Guid? ParentId { get; init; }

    public string? Cursor { get; init; }

    [Range(1, 50)]
    public int PageSize { get; init; } = 20;
}
