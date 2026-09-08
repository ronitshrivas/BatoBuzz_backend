using BatoBuzz.PostInterest.Data;
using BatoBuzz.PostInterest.Dtos;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;
using PostInterestEntity = BatoBuzz.PostInterest.Entities.PostInterest;

namespace BatoBuzz.PostInterest.Services;

public interface IPostInterestService
{
    Task<InterestStateDto> ExpressAsync(ExpressInterestRequest req, CancellationToken ct);
    Task<InterestStateDto> WithdrawAsync(Guid postId, CancellationToken ct);
    Task<InterestStateDto> GetStateAsync(Guid postId, CancellationToken ct);
    Task<InterestCountDto> GetCountAsync(Guid postId, CancellationToken ct);
    Task<IReadOnlyList<PostInterestDto>> GetInterestedUsersAsync(Guid postId, CancellationToken ct);
    Task<IReadOnlyList<PostInterestDto>> GetMyInterestsAsync(CancellationToken ct);
}

public sealed class PostInterestService : IPostInterestService
{
    private readonly PostInterestDbContext _db;
    private readonly ICurrentActor _actor;

    public PostInterestService(PostInterestDbContext db, ICurrentActor actor)
        => (_db, _actor) = (db, actor);

    /// Express interest in an event. Rejects your own post, non-event types, and
    /// a duplicate — matching the app's PostInterestException codes.
    public async Task<InterestStateDto> ExpressAsync(ExpressInterestRequest req, CancellationToken ct)
    {
        var userId = _actor.Id;

        if (req.MerchantId == userId)
            throw new AppException("You can't express interest in your own post.");
        if (!string.Equals(req.PostType, "event", StringComparison.OrdinalIgnoreCase))
            throw new AppException("Interest can only be expressed on event posts.");

        var existing = await _db.Interests
            .FirstOrDefaultAsync(i => i.PostId == req.PostId && i.UserId == userId, ct);
        if (existing is null)
        {
            _db.Interests.Add(new PostInterestEntity
            {
                PostId = req.PostId,
                UserId = userId,
                MerchantId = req.MerchantId,
                PostType = req.PostType,
                UserName = req.UserName,
                UserPhone = req.UserPhone,
                UserEmail = req.UserEmail,
                UserPhoto = req.UserPhoto,
                PostTitle = req.PostTitle,
                PostLocation = req.PostLocation,
                MerchantName = req.MerchantName,
                MerchantPhoto = req.MerchantPhoto,
            });
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                throw AppException.Conflict("You've already expressed interest in this post.");
            }
        }

        return await GetStateAsync(req.PostId, ct);
    }

    public async Task<InterestStateDto> WithdrawAsync(Guid postId, CancellationToken ct)
    {
        var userId = _actor.Id;
        var row = await _db.Interests
            .FirstOrDefaultAsync(i => i.PostId == postId && i.UserId == userId, ct);
        if (row is not null)
        {
            _db.Interests.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
        return await GetStateAsync(postId, ct);
    }

    public async Task<InterestStateDto> GetStateAsync(Guid postId, CancellationToken ct)
    {
        var userId = _actor.IdOrNull;
        var count = await _db.Interests.AsNoTracking().CountAsync(i => i.PostId == postId, ct);
        var has = userId is not null &&
            await _db.Interests.AsNoTracking().AnyAsync(i => i.PostId == postId && i.UserId == userId, ct);
        return new InterestStateDto(postId, has, count);
    }

    public async Task<InterestCountDto> GetCountAsync(Guid postId, CancellationToken ct)
    {
        var count = await _db.Interests.AsNoTracking().CountAsync(i => i.PostId == postId, ct);
        return new InterestCountDto(postId, count);
    }

    public async Task<IReadOnlyList<PostInterestDto>> GetInterestedUsersAsync(Guid postId, CancellationToken ct)
    {
        var rows = await _db.Interests.AsNoTracking()
            .Where(i => i.PostId == postId)
            .OrderByDescending(i => i.InterestedAt)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<PostInterestDto>> GetMyInterestsAsync(CancellationToken ct)
    {
        var userId = _actor.Id;
        var rows = await _db.Interests.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.InterestedAt)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    private static PostInterestDto Map(PostInterestEntity i) => new(
        i.Id, i.PostId, i.UserId, i.MerchantId, i.PostType,
        i.UserName, i.UserPhone, i.UserEmail, i.UserPhoto,
        i.PostTitle, i.PostLocation, i.MerchantName, i.MerchantPhoto,
        i.InterestedAt);
}
