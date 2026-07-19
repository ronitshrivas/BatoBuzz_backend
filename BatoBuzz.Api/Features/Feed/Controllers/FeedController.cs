using BatoBuzz.Feed.Dtos.Common;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Feed.Controllers;

/// Read + engagement surface shared by both apps.
///
/// Browsing is open to anonymous callers (the apps show a feed before login),
/// but anything that records who acted requires a token.
[ApiController]
[Route("api/feed")]
public sealed class FeedController : ControllerBase
{
    private readonly IPostService _posts;
    private readonly ICityService _cities;

    public FeedController(IPostService posts, ICityService cities)
        => (_posts, _cities) = (posts, cities);

    [HttpGet("posts")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeed([FromQuery] FeedQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<PostDto>>.Ok(await _posts.GetFeedAsync(query, ct)));

    [HttpGet("posts/{postId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPost(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<PostDto>.Ok(await _posts.GetByIdAsync(postId, ct)));

    [HttpGet("cities")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCities(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<CityDto>>.Ok(await _cities.GetAllAsync(ct)));

    [HttpPost("posts/{postId:guid}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<LikeResultDto>.Ok(await _posts.ToggleLikeAsync(postId, ct)));

    [HttpPost("posts/{postId:guid}/view")]
    [Authorize]
    public async Task<IActionResult> RecordView(Guid postId, CancellationToken ct)
    {
        await _posts.RecordViewAsync(postId, ct);
        return Ok(ApiResponse<string>.Ok("ok"));
    }

    [HttpPost("posts/{postId:guid}/report")]
    [Authorize]
    public async Task<IActionResult> Report(Guid postId, ReportPostRequest req, CancellationToken ct)
    {
        await _posts.ReportAsync(postId, req, ct);
        return Ok(ApiResponse<string>.Ok("ok", "Thanks — we'll take a look."));
    }

    [HttpGet("posts/{postId:guid}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetComments(
        Guid postId,
        [FromQuery] CommentQuery query,
        [FromServices] ICommentService comments,
        CancellationToken ct)
        => Ok(ApiResponse<PagedResult<CommentDto>>.Ok(await comments.GetAsync(postId, query, ct)));

    [HttpPost("posts/{postId:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(
        Guid postId,
        CreateCommentRequest req,
        [FromServices] ICommentService comments,
        CancellationToken ct)
        => Ok(ApiResponse<CommentDto>.Ok(await comments.AddAsync(postId, req, ct)));

    [HttpPut("posts/{postId:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateComment(
        Guid postId,
        Guid commentId,
        UpdateCommentRequest req,
        [FromServices] ICommentService comments,
        CancellationToken ct)
        => Ok(ApiResponse<CommentDto>.Ok(await comments.UpdateAsync(postId, commentId, req, ct)));

    [HttpDelete("posts/{postId:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(
        Guid postId,
        Guid commentId,
        [FromServices] ICommentService comments,
        CancellationToken ct)
    {
        await comments.DeleteAsync(postId, commentId, ct);
        return Ok(ApiResponse<string>.Ok("ok", "Comment deleted."));
    }
}
