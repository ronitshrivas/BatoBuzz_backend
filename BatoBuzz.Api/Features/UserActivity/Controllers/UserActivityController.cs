using BatoBuzz.Shared.Results;
using BatoBuzz.UserActivity.Dtos;
using BatoBuzz.UserActivity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.UserActivity.Controllers;

/// The signed-in user's own activity (their likes and comments). Read-only
/// aggregation over the feed — replaces the app's UserActivityService.
[ApiController]
[Route("api/user/activity")]
[Authorize]
public sealed class UserActivityController : ControllerBase
{
    private readonly IUserActivityService _svc;
    public UserActivityController(IUserActivityService svc) => _svc = svc;

    /// The caller's likes + comments merged newest-first. `limit` caps the list
    /// (default 50, max 200).
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<UserActivityItemDto>>.Ok(await _svc.GetMyActivityAsync(limit, ct)));
}
