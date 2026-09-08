using BatoBuzz.Follow.Data;
using BatoBuzz.Follow.Dtos;
using Microsoft.EntityFrameworkCore;
using FollowEntity = BatoBuzz.Follow.Entities.Follow;

namespace BatoBuzz.Follow.Services;

public interface IFollowService
{
    Task<FollowStateDto> SetFollowAsync(Guid merchantId, bool follow, CancellationToken ct);
    Task<FollowStateDto> GetStateAsync(Guid merchantId, CancellationToken ct);
    Task<FollowerCountDto> GetFollowerCountAsync(Guid merchantId, CancellationToken ct);
    Task<FollowingIdsDto> GetMyFollowingIdsAsync(CancellationToken ct);
}

public sealed class FollowService : IFollowService
{
    private readonly FollowDbContext _db;
    private readonly ICurrentActor _actor;

    public FollowService(FollowDbContext db, ICurrentActor actor)
        => (_db, _actor) = (db, actor);

    /// Follow or unfollow. Idempotent: following twice keeps one row, unfollowing
    /// something you don't follow is a no-op. A user can't follow themselves.
    public async Task<FollowStateDto> SetFollowAsync(Guid merchantId, bool follow, CancellationToken ct)
    {
        var userId = _actor.Id;
        if (merchantId == default || merchantId == userId)
            return await GetStateAsync(merchantId, ct);

        var existing = await _db.Follows
            .FirstOrDefaultAsync(f => f.UserId == userId && f.MerchantId == merchantId, ct);

        if (follow && existing is null)
        {
            _db.Follows.Add(new FollowEntity { UserId = userId, MerchantId = merchantId });
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* raced another follow — already following */ }
        }
        else if (!follow && existing is not null)
        {
            _db.Follows.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }

        return await GetStateAsync(merchantId, ct);
    }

    public async Task<FollowStateDto> GetStateAsync(Guid merchantId, CancellationToken ct)
    {
        var userId = _actor.IdOrNull;
        var count = await _db.Follows.AsNoTracking().CountAsync(f => f.MerchantId == merchantId, ct);
        var isFollowing = userId is not null &&
            await _db.Follows.AsNoTracking().AnyAsync(f => f.UserId == userId && f.MerchantId == merchantId, ct);
        return new FollowStateDto(merchantId, isFollowing, count);
    }

    public async Task<FollowerCountDto> GetFollowerCountAsync(Guid merchantId, CancellationToken ct)
    {
        var count = await _db.Follows.AsNoTracking().CountAsync(f => f.MerchantId == merchantId, ct);
        return new FollowerCountDto(merchantId, count);
    }

    public async Task<FollowingIdsDto> GetMyFollowingIdsAsync(CancellationToken ct)
    {
        var userId = _actor.Id;
        var ids = await _db.Follows.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.FollowedAt)
            .Select(f => f.MerchantId)
            .ToListAsync(ct);
        return new FollowingIdsDto(ids);
    }
}
