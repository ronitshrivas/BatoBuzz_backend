using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Entities;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Feed.Extensions;
using BatoBuzz.Feed.Services;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Feed.Controllers;

/// Reels = merchant posts of type "reels", played as HLS.
///
/// Upload is a single call that returns immediately: the raw MP4 is saved, a
/// reel post is created with status "processing", and a background worker
/// transcodes it to HLS + a thumbnail. The app polls the reel (or the feed)
/// until reelStatus becomes "ready", then plays reelHlsUrl.
[ApiController]
[Route("api/feed/reels")]
public sealed class ReelsController : ControllerBase
{
    private readonly IPostService _posts;
    private readonly IVideoStorage _videos;
    private readonly IReelJobQueue _queue;
    private readonly ICurrentActor _actor;
    private readonly FeedDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ReelsController(IPostService posts, IVideoStorage videos, IReelJobQueue queue,
        ICurrentActor actor, FeedDbContext db, IWebHostEnvironment env)
        => (_posts, _videos, _queue, _actor, _db, _env) = (posts, videos, queue, actor, db, env);

    /// The vertical reels feed — reel posts only, newest first, cursor-paged.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] string? cityId, [FromQuery] string? cursor,
        [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var query = new FeedQuery
        {
            PostType = "reels",
            CityId = cityId,
            Cursor = cursor,
            PageSize = pageSize < 1 || pageSize > 50 ? 10 : pageSize,
            SortBy = "latest",
        };
        var page = await _posts.GetFeedAsync(query, ct);
        return Ok(ApiResponse<object>.Ok(page));
    }

    /// Poll a single reel's status while it transcodes.
    [HttpGet("{postId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<PostDto>.Ok(await _posts.GetByIdAsync(postId, ct)));

    /// Upload a reel: saves the raw video, creates the reel post as
    /// "processing", queues transcoding, and returns the post right away.
    /// Merchant-only (approved). The caption and city travel with the upload so
    /// the reel is a complete post the moment it's created.
    [HttpPost("upload")]
    [Authorize(Policy = AppPolicies.ApprovedMerchant)]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile video,
        [FromForm] string? caption,
        [FromForm] string? cityId,
        [FromForm] string? cityName,
        [FromForm] string? category,
        CancellationToken ct)
    {
        if (video is null)
            throw new AppException("A video file is required.");

        var folder = Path.Combine("reels", _actor.Id.ToString(), Guid.NewGuid().ToString("N"));
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Save the raw upload; transcoding reads from this on disk.
        var rawUrl = await _videos.SaveVideoAsync(video, folder, $"source_{stamp}", ct);

        var post = new Post
        {
            MerchantId = _actor.Id,
            MerchantName = _actor.Name,
            MerchantPhoto = _actor.Photo,
            PostType = PostType.Reels,
            Body = caption?.Trim() ?? string.Empty,
            Category = category?.Trim() ?? string.Empty,
            CityId = cityId?.Trim() ?? string.Empty,
            CityName = cityName?.Trim() ?? string.Empty,
            ReelCaption = caption?.Trim(),
            ReelVideoUrl = rawUrl,
            ReelStatus = ReelStatus.Processing,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);

        // Resolve the raw file's absolute path for FFmpeg, then queue the job.
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var sourceAbs = Path.Combine(webRoot, rawUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        await _queue.EnqueueAsync(new ReelJob(post.Id, sourceAbs, folder), ct);

        return Ok(ApiResponse<PostDto>.Ok(
            post.ToDto(isLiked: false, isViewed: false, isReported: false),
            "Reel uploaded. It will be ready to play shortly."));
    }
}