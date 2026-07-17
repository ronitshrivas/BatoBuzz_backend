using BatoBuzz.Feed.Dtos.Common;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Services;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Feed.Controllers;

/// Authoring surface — merchant app only.
///
/// Every action requires the ApprovedMerchant policy, so a Pending or Rejected
/// merchant can sign in and see their status screen but cannot publish. That
/// gate lives here rather than in the service because it's an access decision,
/// not a business rule.
[ApiController]
[Route("api/merchant/posts")]
[Authorize(Policy = AppPolicies.ApprovedMerchant)]
public sealed class MerchantPostsController : ControllerBase
{
    private readonly IPostService _posts;
    private readonly ICurrentActor _actor;

    public MerchantPostsController(IPostService posts, ICurrentActor actor)
        => (_posts, _actor) = (posts, actor);

    /// The merchant's own posts. Reuses the feed pipeline with MerchantId
    /// pinned to the caller, so filters, sorting and paging behave identically.
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] FeedQuery query, CancellationToken ct)
    {
        var mine = query with { MerchantId = _actor.Id };
        return Ok(ApiResponse<PagedResult<PostDto>>.Ok(await _posts.GetFeedAsync(mine, ct)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePostRequest req, CancellationToken ct)
        => Ok(ApiResponse<PostDto>.Ok(await _posts.CreateAsync(req, ct), "Post published."));

    [HttpPut("{postId:guid}")]
    public async Task<IActionResult> Update(Guid postId, UpdatePostRequest req, CancellationToken ct)
        => Ok(ApiResponse<PostDto>.Ok(await _posts.UpdateAsync(postId, req, ct), "Post updated."));

    [HttpDelete("{postId:guid}")]
    public async Task<IActionResult> Delete(Guid postId, CancellationToken ct)
    {
        await _posts.DeleteAsync(postId, ct);
        return Ok(ApiResponse<string>.Ok("ok", "Post deleted."));
    }
}
