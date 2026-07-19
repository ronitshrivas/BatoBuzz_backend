using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Dtos.Common;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Entities;
using BatoBuzz.Feed.Extensions;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Feed.Services;

public sealed class CommentService : ICommentService
{
    private readonly FeedDbContext _db;
    private readonly ICurrentActor _actor;

    public CommentService(FeedDbContext db, ICurrentActor actor)
        => (_db, _actor) = (db, actor);

    /// Lists top-level comments, or the replies under one comment when
    /// ParentId is set — the same two-level shape as the old
    /// comments/{id}/replies subcollections.
    public async Task<PagedResult<CommentDto>> GetAsync(Guid postId, CommentQuery query, CancellationToken ct)
    {
        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted, ct);
        if (!postExists) throw AppException.NotFound("This post is no longer available.");

        var q = _db.PostComments.AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted && c.ParentId == query.ParentId);

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var c = CursorCodec.Decode(query.Cursor);
            var at = c.CreatedAt;
            var id = c.Id;

            // Comments read oldest-first (conversation order), so the seek runs
            // forward through time.
            q = q.Where(x =>
                x.CreatedAt > at ||
                (x.CreatedAt == at && x.Id.CompareTo(id) > 0));
        }

        q = q.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id);

        var rows = await q.Take(query.PageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > query.PageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        var viewerId = _actor.IdOrNull;
        var items = rows.Select(c => c.ToDto(viewerId)).ToList();

        var nextCursor = hasMore && rows.Count > 0
            ? CursorCodec.Encode(new FeedCursor(rows[^1].CreatedAt, 0, rows[^1].Id))
            : null;

        return new PagedResult<CommentDto>(items, nextCursor, hasMore);
    }

    /// Adds a comment or a reply. Both apps can comment; the author identity
    /// comes from the token, never the request body.
    public async Task<CommentDto> AddAsync(Guid postId, CreateCommentRequest req, CancellationToken ct)
    {
        var authorId = _actor.Id;

        var text = req.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new AppException("A comment cannot be empty.");

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
            ?? throw AppException.NotFound("This post is no longer available.");

        PostComment? parent = null;
        if (req.ParentId is { } parentId)
        {
            parent = await _db.PostComments
                .FirstOrDefaultAsync(c => c.Id == parentId && c.PostId == postId && !c.IsDeleted, ct)
                ?? throw AppException.NotFound("The comment you're replying to no longer exists.");

            // Threads stay two levels deep, matching the app's UI. A reply to a
            // reply attaches to the same parent rather than nesting further.
            if (parent.ParentId is { } grandParentId)
            {
                parent = await _db.PostComments
                    .FirstOrDefaultAsync(c => c.Id == grandParentId && !c.IsDeleted, ct)
                    ?? throw AppException.NotFound("The comment you're replying to no longer exists.");
            }
        }

        var comment = new PostComment
        {
            PostId = postId,
            ParentId = parent?.Id,
            AuthorId = authorId,
            AuthorType = _actor.Type,
            AuthorName = _actor.Name,
            AuthorPhoto = _actor.Photo,
            Text = text,
        };

        _db.PostComments.Add(comment);

        // Only top-level comments move the post's comment count; replies roll up
        // into their parent, which is what each app's UI displays.
        if (parent is null) post.CommentCount += 1;
        else parent.ReplyCount += 1;

        await _db.SaveChangesAsync(ct);

        return comment.ToDto(authorId);
    }

    public async Task<CommentDto> UpdateAsync(Guid postId, Guid commentId, UpdateCommentRequest req, CancellationToken ct)
    {
        var authorId = _actor.Id;

        var text = req.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new AppException("A comment cannot be empty.");

        var comment = await _db.PostComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId && !c.IsDeleted, ct)
            ?? throw AppException.NotFound("This comment no longer exists.");

        if (comment.AuthorId != authorId)
            throw AppException.Forbidden("You can only edit your own comments.");

        comment.Text = text;
        comment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return comment.ToDto(authorId);
    }

    /// The comment's author can delete it; so can the merchant who owns the
    /// post, since they moderate their own threads.
    public async Task DeleteAsync(Guid postId, Guid commentId, CancellationToken ct)
    {
        var actorId = _actor.Id;

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
            ?? throw AppException.NotFound("This post is no longer available.");

        var comment = await _db.PostComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId && !c.IsDeleted, ct)
            ?? throw AppException.NotFound("This comment no longer exists.");

        var isAuthor = comment.AuthorId == actorId;
        var ownsPost = _actor.IsMerchant && post.MerchantId == actorId;
        if (!isAuthor && !ownsPost)
            throw AppException.Forbidden("You can only delete your own comments.");

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        if (comment.ParentId is { } parentId)
        {
            var parent = await _db.PostComments.FirstOrDefaultAsync(c => c.Id == parentId, ct);
            if (parent is not null) parent.ReplyCount = Math.Max(0, parent.ReplyCount - 1);
        }
        else
        {
            // Deleting a top-level comment takes its replies with it, so the
            // post's count drops by one and the orphans stop being listed.
            var replies = await _db.PostComments
                .Where(c => c.ParentId == commentId && !c.IsDeleted)
                .ToListAsync(ct);

            foreach (var reply in replies)
            {
                reply.IsDeleted = true;
                reply.UpdatedAt = DateTime.UtcNow;
            }

            comment.ReplyCount = 0;
            post.CommentCount = Math.Max(0, post.CommentCount - 1);
        }

        await _db.SaveChangesAsync(ct);
    }
}
