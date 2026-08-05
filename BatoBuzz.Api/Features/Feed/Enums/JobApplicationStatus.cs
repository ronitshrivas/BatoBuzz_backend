namespace BatoBuzz.Feed.Enums;

/// Where an application sits in the merchant's pipeline. Wire values match the
/// app's strings ("pending", "reviewed", "shortlisted", "rejected", "hired").
public enum JobApplicationStatus { Pending = 0, Reviewed = 1, Shortlisted = 2, Rejected = 3, Hired = 4 }

public static class JobApplicationStatusMap
{
    public static string ToWire(this JobApplicationStatus s) => s switch
    {
        JobApplicationStatus.Reviewed => "reviewed",
        JobApplicationStatus.Shortlisted => "shortlisted",
        JobApplicationStatus.Rejected => "rejected",
        JobApplicationStatus.Hired => "hired",
        _ => "pending",
    };

    public static JobApplicationStatus Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "reviewed" => JobApplicationStatus.Reviewed,
        "shortlisted" => JobApplicationStatus.Shortlisted,
        "rejected" => JobApplicationStatus.Rejected,
        "hired" => JobApplicationStatus.Hired,
        _ => JobApplicationStatus.Pending,
    };
}