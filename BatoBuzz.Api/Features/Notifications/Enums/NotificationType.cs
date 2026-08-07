namespace BatoBuzz.Notifications.Enums;

/// Kinds of notification. Wire values match the app's enum names
/// ("like", "comment", "message", "vote", "job_application", "award").
public enum NotificationType { Like = 0, Comment = 1, Message = 2, Vote = 3, JobApplication = 4, Award = 5 }

public static class NotificationTypeMap
{
    public static string ToWire(this NotificationType t) => t switch
    {
        NotificationType.Comment => "comment",
        NotificationType.Message => "message",
        NotificationType.Vote => "vote",
        NotificationType.JobApplication => "job_application",
        NotificationType.Award => "award",
        _ => "like",
    };

    public static NotificationType Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "comment" => NotificationType.Comment,
        "message" => NotificationType.Message,
        "vote" => NotificationType.Vote,
        "job_application" => NotificationType.JobApplication,
        "award" => NotificationType.Award,
        _ => NotificationType.Like,
    };
}