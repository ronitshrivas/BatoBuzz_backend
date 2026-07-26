namespace BatoBuzz.Points.Entities;

/// A user's running points total — the relational form of the Firestore
/// `userPoints/{uid}` document. Kept as a single denormalized row so the
/// leaderboard and standing queries are cheap (no summing history on read).
public class UserPoints
{
    public Guid UserId { get; set; }              // primary key = Identity user id
    public int TotalPoints { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Achievement> Achievements { get; set; } = new();
}

/// An earned badge, stored inline on the points row (matches the app's
/// `achievements` array on the same document).
public class Achievement
{
    public string Tier { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Season { get; set; }
}