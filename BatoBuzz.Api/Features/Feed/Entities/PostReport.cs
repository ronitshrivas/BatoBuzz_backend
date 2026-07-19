using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Entities;

/// Replaces Firestore's `reportedBy` array. One row per (post, reporter).
public class PostReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }
    public Post? Post { get; set; }

    public Guid ReporterId { get; set; }
    public AuthorType ReporterType { get; set; } = AuthorType.User;

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
