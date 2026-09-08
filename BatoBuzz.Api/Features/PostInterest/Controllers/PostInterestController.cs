using BatoBuzz.PostInterest.Dtos;
using BatoBuzz.PostInterest.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.PostInterest.Controllers;

/// "I'm interested" on event posts. Replaces the app's PostInterestService
/// Firestore reads/writes on the `PostInterests` collection.
[ApiController]
[Route("api/post-interest")]
public sealed class PostInterestController : ControllerBase
{
    private readonly IPostInterestService _svc;
    public PostInterestController(IPostInterestService svc) => _svc = svc;

    /// Express interest in an event post.
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Express(ExpressInterestRequest req, CancellationToken ct)
        => Ok(ApiResponse<InterestStateDto>.Ok(await _svc.ExpressAsync(req, ct)));

    /// Withdraw your interest.
    [HttpDelete("posts/{postId:guid}")]
    [Authorize]
    public async Task<IActionResult> Withdraw(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<InterestStateDto>.Ok(await _svc.WithdrawAsync(postId, ct)));

    /// Your interest state + count for a post.
    [HttpGet("posts/{postId:guid}/state")]
    [Authorize]
    public async Task<IActionResult> State(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<InterestStateDto>.Ok(await _svc.GetStateAsync(postId, ct)));

    /// Public interest count for a post.
    [HttpGet("posts/{postId:guid}/count")]
    [AllowAnonymous]
    public async Task<IActionResult> Count(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<InterestCountDto>.Ok(await _svc.GetCountAsync(postId, ct)));

    /// The users who expressed interest in a post (for the post's merchant).
    [HttpGet("posts/{postId:guid}/users")]
    [Authorize]
    public async Task<IActionResult> InterestedUsers(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PostInterestDto>>.Ok(await _svc.GetInterestedUsersAsync(postId, ct)));

    /// The posts the caller has expressed interest in.
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PostInterestDto>>.Ok(await _svc.GetMyInterestsAsync(ct)));
}
