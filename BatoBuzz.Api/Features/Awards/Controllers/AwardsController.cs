using BatoBuzz.Awards.Dtos;
using BatoBuzz.Awards.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Awards.Controllers;

/// The award event — config, participation, voting, leaderboard.
[ApiController]
[Route("api/awards")]
public sealed class AwardsController : ControllerBase
{
    private readonly IAwardService _svc;
    public AwardsController(IAwardService svc) => _svc = svc;

    /// The current award (season, title, whether voting is open). Public.
    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<IActionResult> Config(CancellationToken ct)
        => Ok(ApiResponse<AwardConfigDto?>.Ok(await _svc.GetCurrentConfigAsync(ct)));

    /// Approved participants ranked by votes. Public.
    [HttpGet("leaderboard")]
    [AllowAnonymous]
    public async Task<IActionResult> Leaderboard([FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<LeaderboardParticipantDto>>.Ok(await _svc.GetLeaderboardAsync(limit, ct)));

    // ── Merchant participation ─────────────────────────────────────────────

    /// Apply to (or update your entry in) the current season. Merchant-only.
    [HttpPost("participate")]
    [Authorize]
    public async Task<IActionResult> Participate(SubmitParticipationRequest req, CancellationToken ct)
        => Ok(ApiResponse<ParticipantDto>.Ok(await _svc.SubmitParticipationAsync(req, ct), "Participation submitted."));

    /// The caller's own participation entry (null if none).
    [HttpGet("participate/mine")]
    [Authorize]
    public async Task<IActionResult> MyParticipation(CancellationToken ct)
        => Ok(ApiResponse<ParticipantDto?>.Ok(await _svc.GetMyParticipationAsync(ct)));

    // ── User voting ───────────────────────────────────────────────────────────

    /// Cast the caller's single award vote.
    [HttpPost("vote")]
    [Authorize]
    public async Task<IActionResult> Vote(CastAwardVoteRequest req, CancellationToken ct)
        => Ok(ApiResponse<AwardVoteResult>.Ok(await _svc.CastVoteAsync(req.ParticipantId, ct), "Vote cast."));

    /// Who the caller voted for this season (null if not yet).
    [HttpGet("vote/mine")]
    [Authorize]
    public async Task<IActionResult> MyVote(CancellationToken ct)
        => Ok(ApiResponse<MyAwardVoteDto>.Ok(await _svc.GetMyVoteAsync(ct)));
}