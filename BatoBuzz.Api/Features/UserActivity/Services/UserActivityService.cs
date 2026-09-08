using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Entities;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Feed.Services;
using BatoBuzz.UserActivity.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.UserActivity.Services;

public interface IUserActivityService
{
    Task<IReadOnlyList<UserActivityItemDto>> GetMyActivityAsync(int limit, CancellationToken ct);
}

/// Read-only aggregation of the caller's own likes and comments across the feed.
/// This introduces no new storage: it reads the existing Feed tables (the app
/// derived the same view from a `likedBy` array + a `comments` collection-group
/// query), so it reuses the Feed DbContext and the Feed feature's actor.
public sealed class UserActivityService : IUserActivityService
{
    private readonly FeedDbContext _db;
    private readonly ICurrentActor _actor;

    public UserActivityService(FeedDbContext db, ICurrentActor actor)
        => (_db, _actor) = (db, actor);

    public async Task<IReadOnlyList<UserActivityItemDto>> GetMyActivityAsync(int limit, CancellationToken ct)
    {
        var userId = _actor.Id;
        var take = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);

        // Comments the user wrote (excluding deleted), joined to their post.
        var commentRows = await _db.PostComments.AsNoTracking()
            .Where(c => c.AuthorId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .Join(_db.Posts.AsNoTracking(), c => c.PostId, p => p.Id,
                (c, p) => new { c.PostId, c.Text, c.CreatedAt, Post = p })
            .ToListAsync(ct);

        // Posts the user liked (as a user), joined to the post.
        var likeRows = await _db.PostLikes.AsNoTracking()
            .Where(l => l.ActorId == userId && l.ActorType == AuthorType.User)
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Join(_db.Posts.AsNoTracking(), l => l.PostId, p => p.Id,
                (l, p) => new { l.PostId, l.CreatedAt, Post = p })
            .ToListAsync(ct);

        var items = new List<UserActivityItemDto>(commentRows.Count + likeRows.Count);

        foreach (var r in commentRows)
            items.Add(new UserActivityItemDto(
                "comment", r.PostId, TitleFromPost(r.Post), r.Text, ImageFromPost(r.Post), r.CreatedAt));

        foreach (var r in likeRows)
            items.Add(new UserActivityItemDto(
                "like", r.PostId, TitleFromPost(r.Post), null, ImageFromPost(r.Post), r.CreatedAt));

        // Merge newest-first and cap.
        return items
            .OrderByDescending(i => i.Time)
            .Take(take)
            .ToList();
    }

    /// Readable title for a post: event → job → first line of body → category.
    /// Mirrors UserActivityService.titleFromPost in the app.
    private static string TitleFromPost(Post p)
    {
        if (!string.IsNullOrWhiteSpace(p.EventTitle)) return p.EventTitle!.Trim();
        if (!string.IsNullOrWhiteSpace(p.JobTitle)) return p.JobTitle!.Trim();
        if (!string.IsNullOrWhiteSpace(p.Body))
        {
            var firstLine = p.Body.Split('\n')[0].Trim();
            return firstLine.Length > 60 ? firstLine[..60] + "…" : firstLine;
        }
        return p.Category?.Trim() ?? string.Empty;
    }

    /// Best available thumbnail: generic images → event cover → reel thumbnail.
    private static string? ImageFromPost(Post p)
    {
        var first = p.ImageUrls?.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        if (!string.IsNullOrWhiteSpace(first)) return first;
        if (!string.IsNullOrWhiteSpace(p.EventCoverUrl)) return p.EventCoverUrl;
        if (!string.IsNullOrWhiteSpace(p.ReelThumbnailUrl)) return p.ReelThumbnailUrl;
        return null;
    }
}
