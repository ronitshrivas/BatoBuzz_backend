using BatoBuzz.Follow.Dtos;
using BatoBuzz.Follow.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Follow.Controllers;

/// Users following merchants. Replaces the app's FollowService Firestore writes
/// (mirrored following/followers subcollections + followerCount).
[ApiController]
[Route("api/follow")]
public sealed class FollowController : ControllerBase
{
    private readonly IFollowService _svc;
    public FollowController(IFollowService svc) => _svc = svc;

    /// Follow or unfollow a merchant; returns the fresh state + count.
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Set(SetFollowRequest req, CancellationToken ct)
        => Ok(ApiResponse<FollowStateDto>.Ok(await _svc.SetFollowAsync(req.MerchantId, req.Follow, ct)));

    /// Whether the caller follows this merchant, plus the follower count.
    /// Follower count is public; IsFollowing is false when anonymous.
    [HttpGet("merchants/{merchantId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> State(Guid merchantId, CancellationToken ct)
        => Ok(ApiResponse<FollowStateDto>.Ok(await _svc.GetStateAsync(merchantId, ct)));

    /// Just the public follower count for a merchant.
    [HttpGet("merchants/{merchantId:guid}/count")]
    [AllowAnonymous]
    public async Task<IActionResult> Count(Guid merchantId, CancellationToken ct)
        => Ok(ApiResponse<FollowerCountDto>.Ok(await _svc.GetFollowerCountAsync(merchantId, ct)));

    /// The merchant ids the caller follows.
    [HttpGet("mine/ids")]
    [Authorize]
    public async Task<IActionResult> MyFollowing(CancellationToken ct)
        => Ok(ApiResponse<FollowingIdsDto>.Ok(await _svc.GetMyFollowingIdsAsync(ct)));
}
