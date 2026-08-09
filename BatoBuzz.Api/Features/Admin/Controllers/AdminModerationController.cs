using BatoBuzz.Admin.Dtos;
using BatoBuzz.Admin.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Admin.Controllers;

/// Content moderation, points control, broadcasts, and the audit log.
[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "superadmin")]
public sealed class AdminModerationController : ControllerBase
{
    private readonly IAdminModerationService _mod;
    private readonly IAdminPointsService _points;
    private readonly IAdminBroadcastService _broadcast;
    private readonly IAdminAuditReader _audit;

    public AdminModerationController(
        IAdminModerationService mod, IAdminPointsService points,
        IAdminBroadcastService broadcast, IAdminAuditReader audit)
    {
        _mod = mod; _points = points; _broadcast = broadcast; _audit = audit;
    }

    // ── Content ────────────────────────────────────────────────────────────────

    [HttpGet("posts")]
    public async Task<IActionResult> Posts([FromQuery] string? type, [FromQuery] bool includeDeleted = false,
        [FromQuery] string? cursor = null, [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => Ok(ApiResponse<AdminListPage<AdminPostDto>>.Ok(await _mod.ListPostsAsync(type, includeDeleted, cursor, pageSize, ct)));

    [HttpDelete("posts/{postId:guid}")]
    public async Task<IActionResult> RemovePost(Guid postId, [FromQuery] string? reason, CancellationToken ct)
    {
        await _mod.RemovePostAsync(postId, reason, ct);
        return Ok(ApiResponse<object>.Ok(null, "Post removed."));
    }

    [HttpPost("posts/{postId:guid}/restore")]
    public async Task<IActionResult> RestorePost(Guid postId, CancellationToken ct)
    {
        await _mod.RestorePostAsync(postId, ct);
        return Ok(ApiResponse<object>.Ok(null, "Post restored."));
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> RemoveComment(Guid commentId, CancellationToken ct)
    {
        await _mod.RemoveCommentAsync(commentId, ct);
        return Ok(ApiResponse<object>.Ok(null, "Comment removed."));
    }

    [HttpDelete("ratings/{ratingId:guid}")]
    public async Task<IActionResult> RemoveRating(Guid ratingId, CancellationToken ct)
    {
        await _mod.RemoveRatingAsync(ratingId, ct);
        return Ok(ApiResponse<object>.Ok(null, "Rating removed."));
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports([FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => Ok(ApiResponse<AdminListPage<AdminReportDto>>.Ok(await _mod.ListReportsAsync(cursor, pageSize, ct)));

    [HttpPost("reports/{reportId:guid}/resolve")]
    public async Task<IActionResult> ResolveReport(Guid reportId, CancellationToken ct)
    {
        await _mod.ResolveReportAsync(reportId, ct);
        return Ok(ApiResponse<object>.Ok(null, "Report resolved."));
    }

    // ── Points ─────────────────────────────────────────────────────────────────

    [HttpGet("users/{userId:guid}/points")]
    public async Task<IActionResult> Points(Guid userId, CancellationToken ct)
        => Ok(ApiResponse<AdminPointsDto>.Ok(await _points.GetAsync(userId, ct)));

    [HttpPost("users/{userId:guid}/points/adjust")]
    public async Task<IActionResult> AdjustPoints(Guid userId, AdjustPointsRequest req, CancellationToken ct)
        => Ok(ApiResponse<AdminPointsDto>.Ok(await _points.AdjustAsync(userId, req.Delta, req.Reason, ct)));

    // ── Broadcast ────────────────────────────────────────────────────────────────

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast(BroadcastRequest req, CancellationToken ct)
        => Ok(ApiResponse<BroadcastResult>.Ok(await _broadcast.BroadcastAsync(req.Audience, req.Title, req.Body, ct), "Broadcast sent."));

    // ── Audit ──────────────────────────────────────────────────────────────────

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(ApiResponse<AdminListPage<AuditEntryDto>>.Ok(await _audit.ListAsync(cursor, pageSize, ct)));
}