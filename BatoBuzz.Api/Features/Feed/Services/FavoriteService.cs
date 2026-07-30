
using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Entities;
using BatoBuzz.Feed.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Feed.Services;

/// Saving posts. Mirrors the app's per-user favorites subcollection: a toggle
/// that saves or unsaves, the set of saved post ids (for heart icons across the
/// feed), and the saved posts themselves newest-saved-first.
public sealed class FavoriteService : IFavoriteService
{
    private readonly FeedDbContext _db;
    private readonly ICurrentActor _actor;

    public FavoriteService(FeedDbContext db, ICurrentActor actor)
        => (_db, _actor) = (db, actor);

    /// Save if not saved, unsave if saved. Idempotent per (user, post): the
    /// unique index means a double-tap can't create two rows.
    public async Task<ToggleFavoriteResult> ToggleAsync(Guid postId, CancellationToken ct)
    {
        var userId = _actor.Id;

        var existing = await _db.PostFavorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.PostId == postId, ct);

        if (existing is not null)
        {
            _db.PostFavorites.Remove(existing);
            await _db.SaveChangesAsync(ct);
            return new ToggleFavoriteResult(false);
        }

        _db.PostFavorites.Add(new PostFavorite { UserId = userId, PostId = postId });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the unique index — it's already saved, which is the
            // state the caller wanted anyway.
        }
        return new ToggleFavoriteResult(true);
    }

    /// Just the ids — cheap, for lighting up hearts across a feed the user is
    /// scrolling. Matches the app's watchFavoriteIds set.
    public async Task<IReadOnlyList<Guid>> GetFavoriteIdsAsync(CancellationToken ct)
    {
        var userId = _actor.Id;
        return await _db.PostFavorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.PostId)
            .ToListAsync(ct);
    }

    /// The saved posts themselves, most recently saved first, keyset-paginated
    /// on SavedAt. Skips any favorites whose post was since deleted.
    public async Task<FavoritePostsPage> GetFavoritePostsAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var userId = _actor.Id;
        pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;

        var q = _db.PostFavorites.AsNoTracking().Where(f => f.UserId == userId);
        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out var before))
            q = q.Where(f => f.SavedAt < before);

        var favs = await q.OrderByDescending(f => f.SavedAt).Take(pageSize + 1).ToListAsync(ct);

        var hasMore = favs.Count > pageSize;
        if (hasMore) favs.RemoveAt(favs.Count - 1);

        var nextCursor = hasMore && favs.Count > 0 ? EncodeCursor(favs[^1].SavedAt) : null;

        // Load the posts in one query, then order them to match the saved order.
        var ids = favs.Select(f => f.PostId).ToList();
        var posts = await _db.Posts.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);
        var byId = posts.ToDictionary(p => p.Id);

        // Which of these the user has liked/reported, so the cards render right.
        var likedIds = await _db.PostLikes.AsNoTracking()
            .Where(l => l.ActorId == userId && ids.Contains(l.PostId))
            .Select(l => l.PostId).ToListAsync(ct);
        var likedSet = likedIds.ToHashSet();

        var reportedIds = await _db.PostReports.AsNoTracking()
            .Where(r => r.ReporterId == userId && ids.Contains(r.PostId))
            .Select(r => r.PostId).ToListAsync(ct);
        var reportedSet = reportedIds.ToHashSet();

        var items = new List<PostDto>(favs.Count);
        foreach (var f in favs)
        {
            if (!byId.TryGetValue(f.PostId, out var post)) continue; // post deleted
            items.Add(post.ToDto(
                isLiked: likedSet.Contains(post.Id),
                isViewed: true, // it's in their favorites, they've seen it
                isReported: reportedSet.Contains(post.Id)));
        }

        return new FavoritePostsPage(items, nextCursor, hasMore);
    }

    private static string EncodeCursor(DateTime t)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(t.ToString("O")));

    private static bool TryDecodeCursor(string cursor, out DateTime before)
    {
        before = default;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out before);
        }
        catch { return false; }
    }
}