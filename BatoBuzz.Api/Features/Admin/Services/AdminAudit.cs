using System.Security.Claims;
using BatoBuzz.Admin.Data;
using BatoBuzz.Admin.Entities;


namespace BatoBuzz.Admin.Services;

public sealed class AdminAudit : IAdminAudit
{
    private readonly AdminDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AdminAudit(AdminDbContext db, IHttpContextAccessor http)
        => (_db, _http) = (db, http);

    public async Task LogAsync(string action, string targetType, string? targetId, string? detail, CancellationToken ct)
    {
        var p = _http.HttpContext?.User;
        Guid.TryParse(p?.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId);
        var name = p?.FindFirst("display_name")?.Value ?? p?.Identity?.Name ?? "admin";

        _db.AuditEntries.Add(new AdminAuditEntry
        {
            ActorId = actorId,
            ActorName = name,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Detail = detail,
        });
        await _db.SaveChangesAsync(ct);
    }
}