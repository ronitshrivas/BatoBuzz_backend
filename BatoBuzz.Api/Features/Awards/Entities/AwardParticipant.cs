using BatoBuzz.Awards.Enums;

namespace BatoBuzz.Awards.Entities;

/// A merchant participating in an award season. One row per (season, merchant).
/// voteCount is denormalized for a cheap leaderboard.
public class AwardParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Season { get; set; } = string.Empty;
    public Guid MerchantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Photo { get; set; }
    public string? Pitch { get; set; }               // why they should win

    public ParticipationStatus Status { get; set; } = ParticipationStatus.Pending;
    public int VoteCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}