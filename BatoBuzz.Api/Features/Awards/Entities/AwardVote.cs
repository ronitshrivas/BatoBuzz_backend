namespace BatoBuzz.Awards.Entities;

/// A user's vote in an award season. One vote per (season, user) — a user backs
/// a single participant per season. The primary key (Season, VoterId) enforces
/// it, matching the app's one-vote rule.
public class AwardVote
{
    public string Season { get; set; } = string.Empty;
    public Guid VoterId { get; set; }
    public Guid ParticipantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}