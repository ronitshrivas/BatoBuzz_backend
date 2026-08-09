namespace BatoBuzz.Admin.Services;

/// Records super-admin actions. Call after a successful mutation.
public interface IAdminAudit
{
    Task LogAsync(string action, string targetType, string? targetId, string? detail, CancellationToken ct);
}