using BatoBuzz.Awards.Dtos;
using BatoBuzz.Awards.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Awards.Controllers;

/// Admin management of the award event: set the config, review participants.
[ApiController]
[Route("api/admin/awards")]
[Authorize(Roles = "admin")]
public sealed class AdminAwardsController : ControllerBase
{
    private readonly IAwardService _svc;
    public AdminAwardsController(IAwardService svc) => _svc = svc;

    /// Create or update an award season and set which is active.
    [HttpPut("config")]
    public async Task<IActionResult> SetConfig(UpsertConfigRequest req, CancellationToken ct)
        => Ok(ApiResponse<AwardConfigDto>.Ok(await _svc.UpsertConfigAsync(req, ct), "Award configured."));

    /// Participants awaiting approval for the current season.
    [HttpGet("participants/pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ParticipantDto>>.Ok(await _svc.GetPendingAsync(ct)));

    /// Approve or reject a participant.
    [HttpPost("participants/{participantId:guid}/review")]
    public async Task<IActionResult> Review(Guid participantId, ReviewParticipantRequest req, CancellationToken ct)
        => Ok(ApiResponse<ParticipantDto>.Ok(await _svc.ReviewAsync(participantId, req.Approve, ct)));
}