namespace BatoBuzz.Awards.Enums;

/// A participant's state in an award event. pending → approved (shows up for
/// voting) or rejected. Wire values match the app ("pending"/"approved"/"rejected").
public enum ParticipationStatus { Pending = 0, Approved = 1, Rejected = 2 }

public static class ParticipationStatusMap
{
    public static string ToWire(this ParticipationStatus s) => s switch
    {
        ParticipationStatus.Approved => "approved",
        ParticipationStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static ParticipationStatus Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "approved" => ParticipationStatus.Approved,
        "rejected" => ParticipationStatus.Rejected,
        _ => ParticipationStatus.Pending,
    };
}