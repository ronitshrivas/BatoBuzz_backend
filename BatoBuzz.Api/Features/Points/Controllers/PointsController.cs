using BatoBuzz.Points.Dtos;
using BatoBuzz.Points.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Points.Controllers;

/// Points, history and the leaderboard.
///
/// Awarding is server-side and identity comes from the token, so a client can't
/// grant points to another user or invent an amount — it names the action, the
/// server decides what it's worth.
[ApiController]
[Route("api/points")]
public sealed class PointsController : ControllerBase
{
    private readonly IPointsService _svc;
    public PointsController(IPointsService svc) => _svc = svc;

    /// The caller's points total + achievements.
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => Ok(ApiResponse<UserPointsDto>.Ok(await _svc.GetMyPointsAsync(ct)));

    /// Points earned today (UTC), for the daily figure on the points screen.
    [HttpGet("me/today")]
    [Authorize]
    public async Task<IActionResult> Today(CancellationToken ct)
        => Ok(ApiResponse<int>.Ok(await _svc.GetPointsTodayAsync(ct)));

    /// Paged history, newest first.
    [HttpGet("me/history")]
    [Authorize]
    public async Task<IActionResult> History([FromQuery] string? cursor,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PointHistoryPage>.Ok(await _svc.GetHistoryAsync(cursor, pageSize, ct)));

    /// The caller's rank, the leader's score, and their own — the standing banner.
    [HttpGet("me/standing")]
    [Authorize]
    public async Task<IActionResult> Standing(CancellationToken ct)
        => Ok(ApiResponse<MyStandingDto>.Ok(await _svc.GetMyStandingAsync(ct)));

    /// Top users. Readable without auth so the leaderboard can be shown publicly.
    [HttpGet("leaderboard")]
    [AllowAnonymous]
    public async Task<IActionResult> Leaderboard([FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<LeaderboardEntryDto>>.Ok(await _svc.GetLeaderboardAsync(limit, ct)));

    /// Award points for an action. Idempotent per (user, action, target):
    /// calling it twice for the same like earns points once.
    [HttpPost("award")]
    [Authorize]
    public async Task<IActionResult> Award(PointActionRequest req, CancellationToken ct)
        => Ok(ApiResponse<PointActionResult>.Ok(await _svc.AwardAsync(req, ct)));

    /// Reverse an awarded action (e.g. un-like).
    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke(PointActionRequest req, CancellationToken ct)
        => Ok(ApiResponse<PointActionResult>.Ok(await _svc.RevokeAsync(req, ct)));
}