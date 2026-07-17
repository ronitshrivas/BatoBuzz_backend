using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Dtos.Common;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Entities;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Feed.Extensions;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Feed.Services;

public sealed class PostService : IPostService
{
    private readonly FeedDbContext _db;
    private readonly ICurrentActor _actor;

    public PostService(FeedDbContext db, ICurrentActor actor)
        => (_db, _actor) = (db, actor);

    // ── Read ────────────────────────────────────────────────────────────────

    public async Task<PagedResult<PostDto>> GetFeedAsync(FeedQuery query, CancellationToken ct)
    {
        var sort = EnumMapping.ToFeedSort(query.SortBy);
        var postType = EnumMapping.ToPostTypeOrNull(query.PostType);

        var q = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.CityId))
            q = q.Where(p => p.CityId == query.CityId);

        if (postType is { } pt)
            q = q.Where(p => p.PostType == pt);

        // Matches the Flutter filter: ad categories only narrow ad posts.
        if (postType == PostType.Ads && !string.IsNullOrWhiteSpace(query.AdsCategory))
            q = q.Where(p => p.AdsCategory == query.AdsCategory);

        if (query.MerchantId is { } merchantId)
            q = q.Where(p => p.MerchantId == merchantId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            q = q.Where(p =>
                EF.Functions.ILike(p.Body, term) ||
                EF.Functions.ILike(p.MerchantName, term) ||
                (p.JobTitle != null && EF.Functions.ILike(p.JobTitle, term)) ||
                (p.EventTitle != null && EF.Functions.ILike(p.EventTitle, term)));
        }

        // A caller should never see a post they reported — that was the point of
        // reporting it. Firestore did this client-side against `reportedBy`;
        // doing it in SQL means a reported post can't leak through pagination.
        if (_actor.IdOrNull is { } viewerId)
            q = q.Where(p => !p.Reports.Any(r => r.ReporterId == viewerId));

        q = ApplyCursor(q, sort, query.Cursor);
        q = ApplySort(q, sort);

        // Fetch one extra row to learn whether another page exists without a
        // second COUNT query over the whole filtered set.
        var rows = await q.Take(query.PageSize + 1).ToListAsync(ct);

        var hasMore = rows.Count > query.PageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        var dtos = await ToDtosAsync(rows, ct);
        var nextCursor = hasMore && rows.Count > 0
            ? CursorCodec.Encode(BuildCursor(rows[^1], sort))
            : null;

        return new PagedResult<PostDto>(dtos, nextCursor, hasMore);
    }

    public async Task<PostDto> GetByIdAsync(Guid postId, CancellationToken ct)
    {
        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
            ?? throw AppException.NotFound("This post is no longer available.");

        var dtos = await ToDtosAsync(new List<Post> { post }, ct);
        return dtos[0];
    }

    // ── Write ───────────────────────────────────────────────────────────────

    public async Task<PostDto> CreateAsync(CreatePostRequest req, CancellationToken ct)
    {
        RequireMerchant();

        var postType = EnumMapping.ToPostType(req.PostType);
        ValidateForType(postType, req);

        var post = new Post
        {
            MerchantId = _actor.Id,
            MerchantName = _actor.Name,
            MerchantPhoto = _actor.Photo,
            PostType = postType,
        };

        Apply(post, req, postType);
        post.CreatedAt = DateTime.UtcNow;

        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);

        return post.ToDto(isLiked: false, isViewed: false, isReported: false);
    }

    public async Task<PostDto> UpdateAsync(Guid postId, UpdatePostRequest req, CancellationToken ct)
    {
        RequireMerchant();

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
            ?? throw AppException.NotFound("This post is no longer available.");

        if (post.MerchantId != _actor.Id)
            throw AppException.Forbidden("You can only edit your own posts.");

        var postType = EnumMapping.ToPostType(req.PostType);
        ValidateForType(postType, req);

        post.PostType = postType;
        Apply(post, req, postType);
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dtos = await ToDtosAsync(new List<Post> { post }, ct);
        return dtos[0];
    }

    /// Soft delete: comments and likes stay on the row, and any client holding
    /// the id gets a clean 404 rather than a dangling reference.
    public async Task DeleteAsync(Guid postId, CancellationToken ct)
    {
        RequireMerchant();

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
            ?? throw AppException.NotFound("This post is no longer available.");

        if (post.MerchantId != _actor.Id)
            throw AppException.Forbidden("You can only delete your own posts.");

        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Engagement ──────────────────────────────────────────────────────────

    /// Toggles the caller's like and returns the reconciled state.
    ///
    /// The unique index on (PostId, ActorId) is the source of truth: two taps
    /// racing each other can't produce two likes, and the counter is adjusted
    /// in the same transaction as the row so the two can't drift apart.
    public async Task<LikeResultDto> ToggleLikeAsync(Guid postId, CancellationToken ct)
    {
        var actorId = _actor.Id;

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
            ?? throw AppException.NotFound("This post is no longer available.");

        var existing = await _db.PostLikes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.ActorId == actorId, ct);

        bool isLiked;
        if (existing is null)
        {
            _db.PostLikes.Add(new PostLike
            {
                PostId = postId,
                ActorId = actorId,
                ActorType = _actor.Type,
            });
            post.LikeCount += 1;
            isLiked = true;
        }
        else
        {
            _db.PostLikes.Remove(existing);
            // Clamp: a counter that somehow drifted must never go negative.
            post.LikeCount = Math.Max(0, post.LikeCount - 1);
            isLiked = false;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (isLiked)
        {
            // Lost a race against a concurrent like from the same actor. The
            // row already exists, so the intent is satisfied — report the
            // settled truth rather than surfacing a spurious 500.
            return await ReadLikeStateAsync(postId, actorId, ct);
        }

        return new LikeResultDto(postId, isLiked, post.LikeCount);
    }

    /// Idempotent per viewer, mirroring the old `viewedBy` transaction. A
    /// merchant viewing their own post is not counted.
    public async Task RecordViewAsync(Guid postId, CancellationToken ct)
    {
        var viewerId = _actor.Id;

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
            ?? throw AppException.NotFound("This post is no longer available.");

        if (post.MerchantId == viewerId) return;

        var seen = await _db.PostViews
            .AnyAsync(v => v.PostId == postId && v.ViewerId == viewerId, ct);
        if (seen) return;

        _db.PostViews.Add(new PostView
        {
            PostId = postId,
            ViewerId = viewerId,
            ViewerType = _actor.Type,
        });
        post.ViewCount += 1;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent first view from the same viewer; already counted.
        }
    }

    public async Task ReportAsync(Guid postId, ReportPostRequest req, CancellationToken ct)
    {
        var reporterId = _actor.Id;

        var exists = await _db.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted, ct);
        if (!exists) throw AppException.NotFound("This post is no longer available.");

        var already = await _db.PostReports
            .AnyAsync(r => r.PostId == postId && r.ReporterId == reporterId, ct);
        if (already) return;

        _db.PostReports.Add(new PostReport
        {
            PostId = postId,
            ReporterId = reporterId,
            ReporterType = _actor.Type,
            Reason = req.Reason.Trim(),
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Duplicate report from the same actor; nothing to do.
        }
    }

    // ── Internals ───────────────────────────────────────────────────────────

    private void RequireMerchant()
    {
        if (!_actor.IsMerchant)
            throw AppException.Forbidden("Only merchants can publish posts.");
    }

    private async Task<LikeResultDto> ReadLikeStateAsync(Guid postId, Guid actorId, CancellationToken ct)
    {
        var count = await _db.Posts.AsNoTracking()
            .Where(p => p.Id == postId)
            .Select(p => p.LikeCount)
            .FirstOrDefaultAsync(ct);

        var liked = await _db.PostLikes.AsNoTracking()
            .AnyAsync(l => l.PostId == postId && l.ActorId == actorId, ct);

        return new LikeResultDto(postId, liked, count);
    }

    /// Resolves like/view/report state for a whole page in three queries rather
    /// than three per post.
    private async Task<List<PostDto>> ToDtosAsync(List<Post> posts, CancellationToken ct)
    {
        if (posts.Count == 0) return new List<PostDto>();

        var viewerId = _actor.IdOrNull;
        if (viewerId is null)
        {
            return posts
                .Select(p => p.ToDto(isLiked: false, isViewed: false, isReported: false))
                .ToList();
        }

        var ids = posts.Select(p => p.Id).ToList();

        var likedIds = await _db.PostLikes.AsNoTracking()
            .Where(l => ids.Contains(l.PostId) && l.ActorId == viewerId)
            .Select(l => l.PostId)
            .ToListAsync(ct);

        var viewedIds = await _db.PostViews.AsNoTracking()
            .Where(v => ids.Contains(v.PostId) && v.ViewerId == viewerId)
            .Select(v => v.PostId)
            .ToListAsync(ct);

        var reportedIds = await _db.PostReports.AsNoTracking()
            .Where(r => ids.Contains(r.PostId) && r.ReporterId == viewerId)
            .Select(r => r.PostId)
            .ToListAsync(ct);

        var likedSet = likedIds.ToHashSet();
        var viewedSet = viewedIds.ToHashSet();
        var reportedSet = reportedIds.ToHashSet();

        return posts
            .Select(p => p.ToDto(
                isLiked: likedSet.Contains(p.Id),
                isViewed: viewedSet.Contains(p.Id),
                isReported: reportedSet.Contains(p.Id)))
            .ToList();
    }

    private static IQueryable<Post> ApplySort(IQueryable<Post> q, FeedSort sort) => sort switch
    {
        // Id is the tiebreaker in every ordering. Without it, rows sharing a
        // sort value have no stable order, and keyset pagination would skip or
        // repeat them at page boundaries.
        FeedSort.Oldest => q.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
        FeedSort.MostViewed => q.OrderByDescending(p => p.ViewCount)
                                .ThenByDescending(p => p.CreatedAt)
                                .ThenBy(p => p.Id),
        FeedSort.ForYou => q.OrderByDescending(p => p.LikeCount)
                            .ThenByDescending(p => p.CreatedAt)
                            .ThenBy(p => p.Id),
        _ => q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
    };

    private static IQueryable<Post> ApplyCursor(IQueryable<Post> q, FeedSort sort, string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return q;

        var c = CursorCodec.Decode(cursor);

        // Hoisted out of the expression trees below so EF sees plain constants.
        var at = c.CreatedAt;
        var id = c.Id;
        var score = (int)c.Score;

        // Each clause is the seek predicate for its ordering — "everything
        // strictly after the last row I sent you", expressed so Postgres can
        // still use the composite index. Guid.CompareTo is translated by
        // Npgsql to a native uuid comparison.
        return sort switch
        {
            FeedSort.Oldest => q.Where(p =>
                p.CreatedAt > at ||
                (p.CreatedAt == at && p.Id.CompareTo(id) > 0)),

            FeedSort.MostViewed => q.Where(p =>
                p.ViewCount < score ||
                (p.ViewCount == score && p.CreatedAt < at) ||
                (p.ViewCount == score && p.CreatedAt == at && p.Id.CompareTo(id) > 0)),

            FeedSort.ForYou => q.Where(p =>
                p.LikeCount < score ||
                (p.LikeCount == score && p.CreatedAt < at) ||
                (p.LikeCount == score && p.CreatedAt == at && p.Id.CompareTo(id) > 0)),

            _ => q.Where(p =>
                p.CreatedAt < at ||
                (p.CreatedAt == at && p.Id.CompareTo(id) > 0)),
        };
    }

    private static FeedCursor BuildCursor(Post last, FeedSort sort) => sort switch
    {
        FeedSort.MostViewed => new FeedCursor(last.CreatedAt, last.ViewCount, last.Id),
        FeedSort.ForYou => new FeedCursor(last.CreatedAt, last.LikeCount, last.Id),
        _ => new FeedCursor(last.CreatedAt, 0, last.Id),
    };

    /// A post's required fields depend entirely on its type — a job with no
    /// title or an event with no date renders as a broken card in the app, so
    /// they're rejected at the edge instead of persisted.
    private static void ValidateForType(PostType type, CreatePostRequest req)
    {
        switch (type)
        {
            case PostType.Job:
                if (string.IsNullOrWhiteSpace(req.JobTitle))
                    throw new AppException("A job post needs a job title.");
                if (req.SalaryFrom is { } from && req.SalaryTo is { } to && from > to)
                    throw new AppException("Salary range is reversed: 'from' is higher than 'to'.");
                break;

            case PostType.Event:
                if (string.IsNullOrWhiteSpace(req.EventTitle))
                    throw new AppException("An event post needs an event title.");
                if (req.EventDate is null)
                    throw new AppException("An event post needs an event date.");
                break;

            case PostType.Reels:
                if (string.IsNullOrWhiteSpace(req.ReelVideoUrl) && string.IsNullOrWhiteSpace(req.ReelsUrl))
                    throw new AppException("A reel needs a video.");
                break;

            case PostType.Ads:
                if (string.IsNullOrWhiteSpace(req.Post) && req.ImageUrls.Count == 0)
                    throw new AppException("An ad needs either text or at least one image.");
                break;
        }

        if (req.Price is < 0 || req.PreviousPrice is < 0 || req.DiscountedPrice is < 0)
            throw new AppException("Prices cannot be negative.");
    }

    private static void Apply(Post post, CreatePostRequest req, PostType type)
    {
        post.Category = req.Category.Trim();
        post.Body = req.Post.Trim();
        post.ImageUrls = req.ImageUrls;
        post.VideoUrl = req.VideoUrl;
        post.ReelsUrl = req.ReelsUrl;

        post.BusinessAddress = req.BusinessAddress.Trim();
        post.CityId = req.CityId.Trim();
        post.CityName = req.CityName.Trim();
        post.BusinessCategory = req.BusinessCategory.Trim();

        // Only ads carry an ad category; keeping it null elsewhere stops the
        // (PostType, AdsCategory) index from filling with meaningless rows.
        post.AdsCategory = type == PostType.Ads ? req.AdsCategory?.Trim() : null;

        post.Price = req.Price;
        post.PreviousPrice = req.PreviousPrice;
        post.DiscountedPrice = req.DiscountedPrice;

        post.ReelCaption = req.ReelCaption;
        post.ReelVideoUrl = req.ReelVideoUrl;
        post.ReelThumbnailUrl = req.ReelThumbnailUrl;

        post.JobTitle = req.JobTitle;
        post.CompanyName = req.CompanyName;
        post.JobLocation = req.JobLocation;
        post.SalaryFrom = req.SalaryFrom;
        post.SalaryTo = req.SalaryTo;
        post.JobDescription = req.JobDescription;
        post.JobSkills = req.JobSkills;
        post.JobPerks = req.JobPerks;
        post.EmploymentType = req.EmploymentType;
        post.WorkMode = req.WorkMode;
        post.ExperienceLevel = req.ExperienceLevel;
        post.IsUrgent = req.IsUrgent;
        post.AllowPhoneCalls = req.AllowPhoneCalls;
        post.AllowWhatsApp = req.AllowWhatsApp;
        post.ContactPhone = req.ContactPhone;
        post.ContactEmail = req.ContactEmail;
        post.JobImageUrls = req.JobImageUrls;

        post.EventTitle = req.EventTitle;
        post.EventDescription = req.EventDescription;
        post.EventLocation = req.EventLocation;
        post.EventDate = req.EventDate;
        post.EventCoverUrl = req.EventCoverUrl;
        post.EventTicketType = req.EventTicketType;
        post.EventPrice = req.EventPrice;
    }
}
