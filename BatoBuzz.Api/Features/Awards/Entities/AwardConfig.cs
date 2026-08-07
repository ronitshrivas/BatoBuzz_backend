namespace BatoBuzz.Awards.Entities;

/// The current award event. A single active row drives everything: which season
/// participants join, whether voting is open, and the window. Mirrors the app's
/// awardConfig/current document.
public class AwardConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Season { get; set; } = string.Empty;      // e.g. "2026-spring"
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool VotingOpen { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}