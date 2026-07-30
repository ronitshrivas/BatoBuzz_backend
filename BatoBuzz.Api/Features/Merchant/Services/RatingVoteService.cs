using BatoBuzz.Merchant.Data;
using BatoBuzz.Merchant.Dtos;
using BatoBuzz.Merchant.Entities;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Merchant.Services;

public interface IRatingVoteService
{
    Task<MerchantRatingSummaryDto> SubmitRatingAsync(Guid merchantId, int rating, CancellationToken ct);
    Task<MerchantRatingSummaryDto> GetRatingSummaryAsync(Guid merchantId, CancellationToken ct);
    Task<VoteResult> CastVoteAsync(Guid merchantId, CancellationToken ct);
    Task<MyVoteDto> GetMyVoteAsync(CancellationToken ct);
    Task<MerchantVoteCountDto> GetVoteCountAsync(Guid merchantId, CancellationToken ct);
}

public sealed class RatingVoteService : IRatingVoteService
{
    private readonly MerchantDbContext _db;
    private readonly ICurrentActor _actor;

    public RatingVoteService(MerchantDbContext db, ICurrentActor actor)
        => (_db, _actor) = (db, actor);

    // ── Ratings ──────────────────────────────────────────────────────────────

    /// Upsert the caller's 1-5 rating for a merchant. Re-rating updates the same
    /// row (one rating per user per merchant), then returns the fresh average.
    public async Task<MerchantRatingSummaryDto> SubmitRatingAsync(Guid merchantId, int rating, CancellationToken ct)
    {
        var value = Math.Clamp(rating, 1, 5);
        var userId = _actor.Id;

        var existing = await _db.MerchantRatings
            .FirstOrDefaultAsync(r => r.MerchantId == merchantId && r.UserId == userId, ct);

        if (existing is null)
        {
            _db.MerchantRatings.Add(new MerchantRating
            {
                MerchantId = merchantId,
                UserId = userId,
                Rating = value,
            });
        }
        else
        {
            existing.Rating = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Race on the unique index — reload and set the value.
            var row = await _db.MerchantRatings
                .FirstAsync(r => r.MerchantId == merchantId && r.UserId == userId, ct);
            row.Rating = value; row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return await GetRatingSummaryAsync(merchantId, ct);
    }

    public async Task<MerchantRatingSummaryDto> GetRatingSummaryAsync(Guid merchantId, CancellationToken ct)
    {
        var userId = _actor.IdOrNull;

        var stats = await _db.MerchantRatings.AsNoTracking()
            .Where(r => r.MerchantId == merchantId)
            .GroupBy(r => r.MerchantId)
            .Select(g => new { Avg = g.Average(x => (double)x.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        int? mine = null;
        if (userId is not null)
        {
            mine = await _db.MerchantRatings.AsNoTracking()
                .Where(r => r.MerchantId == merchantId && r.UserId == userId)
                .Select(r => (int?)r.Rating)
                .FirstOrDefaultAsync(ct);
        }

        var avg = stats is null ? 0 : Math.Round(stats.Avg, 2);
        return new MerchantRatingSummaryDto(merchantId, avg, stats?.Count ?? 0, mine);
    }

    // ── Award voting ─────────────────────────────────────────────────────────

    /// Cast the caller's single award vote. One vote per user, ever — trying to
    /// vote again (even for the same merchant) is rejected, matching the app's
    /// AlreadyVotedException. The vote and the count update are one transaction.
    public async Task<VoteResult> CastVoteAsync(Guid merchantId, CancellationToken ct)
    {
        var userId = _actor.Id;

        var already = await _db.MerchantVotes.AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId, ct);
        if (already is not null)
            throw new AppException("You've already cast your vote and it can't be changed.", 409);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.MerchantVotes.Add(new MerchantVote { UserId = userId, MerchantId = merchantId });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the race — someone recorded this user's vote first.
            await tx.RollbackAsync(ct);
            throw new AppException("You've already cast your vote and it can't be changed.", 409);
        }

        await tx.CommitAsync(ct);

        var count = await _db.MerchantVotes.AsNoTracking()
            .CountAsync(v => v.MerchantId == merchantId, ct);
        return new VoteResult(true, merchantId, count);
    }

    public async Task<MyVoteDto> GetMyVoteAsync(CancellationToken ct)
    {
        var userId = _actor.Id;
        var voted = await _db.MerchantVotes.AsNoTracking()
            .Where(v => v.UserId == userId)
            .Select(v => (Guid?)v.MerchantId)
            .FirstOrDefaultAsync(ct);
        return new MyVoteDto(voted);
    }

    public async Task<MerchantVoteCountDto> GetVoteCountAsync(Guid merchantId, CancellationToken ct)
    {
        var count = await _db.MerchantVotes.AsNoTracking()
            .CountAsync(v => v.MerchantId == merchantId, ct);
        return new MerchantVoteCountDto(merchantId, count);
    }
}