namespace BatoBuzz.Admin.Entities;

/// One record of a super-admin action, for accountability. When a single owner
/// can change anything, this log is the safety net: who did what, to whom, when.
public class AdminAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ActorId { get; set; }             // the admin who acted
    public string ActorName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;   // e.g. "user.suspend", "post.remove"
    public string TargetType { get; set; } = string.Empty; // "user" | "merchant" | "post" | ...
    public string? TargetId { get; set; }
    public string? Detail { get; set; }           // free-form context (reason, before/after)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}