using System.Security.Claims;
using BatoBuzz.Awards.Data;
using BatoBuzz.Awards.Dtos;
using BatoBuzz.Awards.Entities;
using BatoBuzz.Awards.Enums;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Awards.Services;

public interface IAwardService
{
    Task<AwardConfigDto?> GetCurrentConfigAsync(CancellationToken ct);
    Task<AwardConfigDto> UpsertConfigAsync(UpsertConfigRequest req, CancellationToken ct);

    Task<ParticipantDto> SubmitParticipationAsync(SubmitParticipationRequest req, CancellationToken ct);
    Task<ParticipantDto?> GetMyParticipationAsync(CancellationToken ct);
    Task<IReadOnlyList<ParticipantDto>> GetPendingAsync(CancellationToken ct);
    Task<ParticipantDto> ReviewAsync(Guid participantId, bool approve, CancellationToken ct);
    Task<IReadOnlyList<LeaderboardParticipantDto>> GetLeaderboardAsync(int limit, CancellationToken ct);

    Task<AwardVoteResult> CastVoteAsync(Guid participantId, CancellationToken ct);
    Task<MyAwardVoteDto> GetMyVoteAsync(CancellationToken ct);
}

/// The award-event system: an active config (season + voting window), merchants
/// applying to participate (admin-approved), and users casting one vote per
/// season for an approved participant.
public sealed class AwardService : IAwardService
{
    private readonly AwardsDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AwardService(AwardsDbContext db, IHttpContextAccessor http)
        => (_db, _http) = (db, http);

    // ── Config ────────────────────────────────────────────────────────────────

    public async Task<AwardConfigDto?> GetCurrentConfigAsync(CancellationToken ct)
    {
        var c = await _db.Configs.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        return c is null ? null : ToDto(c);
    }

    /// Admin sets the active award. Making one active deactivates the others so
    /// there's always a single "current" season.
    public async Task<AwardConfigDto> UpsertConfigAsync(UpsertConfigRequest req, CancellationToken ct)
    {
        var season = req.Season.Trim();

        var config = await _db.Configs.FirstOrDefaultAsync(c => c.Season == season, ct);
        if (config is null)
        {
            config = new AwardConfig { Season = season };
            _db.Configs.Add(config);
        }

        config.Title = req.Title.Trim();
        config.IsActive = req.IsActive;
        config.VotingOpen = req.VotingOpen;
        config.StartsAt = req.StartsAt;
        config.EndsAt = req.EndsAt;
        config.UpdatedAt = DateTime.UtcNow;

        if (req.IsActive)
        {
            // Only one active season at a time.
            await _db.Configs
                .Where(c => c.Season != season && c.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), ct);
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(config);
    }

    // ── Participation ───────────────────────────────────────────────────────────

    /// A merchant applies to the current season. Re-submitting updates their
    /// entry (name/photo/pitch) as long as it's still pending.
    public async Task<ParticipantDto> SubmitParticipationAsync(SubmitParticipationRequest req, CancellationToken ct)
    {
        var merchantId = RequireMerchant();
        var season = await RequireActiveSeasonAsync(ct);

        var existing = await _db.Participants
            .FirstOrDefaultAsync(p => p.Season == season && p.MerchantId == merchantId, ct);

        if (existing is null)
        {
            existing = new AwardParticipant { Season = season, MerchantId = merchantId };
            _db.Participants.Add(existing);
        }
        else if (existing.Status == ParticipationStatus.Approved)
        {
            throw new AppException("You're already an approved participant.", 409);
        }

        existing.Name = req.Name.Trim();
        existing.Photo = req.Photo;
        existing.Pitch = req.Pitch?.Trim();
        existing.UpdatedAt = DateTime.UtcNow;

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            existing = await _db.Participants
                .FirstAsync(p => p.Season == season && p.MerchantId == merchantId, ct);
        }
        return ToDto(existing);
    }

    public async Task<ParticipantDto?> GetMyParticipationAsync(CancellationToken ct)
    {
        var merchantId = RequireMerchant();
        var season = await CurrentSeasonOrNullAsync(ct);
        if (season is null) return null;

        var p = await _db.Participants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Season == season && x.MerchantId == merchantId, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<IReadOnlyList<ParticipantDto>> GetPendingAsync(CancellationToken ct)
    {
        var season = await RequireActiveSeasonAsync(ct);
        var rows = await _db.Participants.AsNoTracking()
            .Where(p => p.Season == season && p.Status == ParticipationStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ParticipantDto> ReviewAsync(Guid participantId, bool approve, CancellationToken ct)
    {
        var p = await _db.Participants.FirstOrDefaultAsync(x => x.Id == participantId, ct)
            ?? throw AppException.NotFound("Participant not found.");
        p.Status = approve ? ParticipationStatus.Approved : ParticipationStatus.Rejected;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    /// Approved participants ranked by votes; ties break toward the earlier
    /// entry so whoever reached the score first ranks higher.
    public async Task<IReadOnlyList<LeaderboardParticipantDto>> GetLeaderboardAsync(int limit, CancellationToken ct)
    {
        var season = await RequireActiveSeasonAsync(ct);
        limit = limit is < 1 or > 100 ? 20 : limit;

        var rows = await _db.Participants.AsNoTracking()
            .Where(p => p.Season == season && p.Status == ParticipationStatus.Approved)
            .OrderByDescending(p => p.VoteCount).ThenBy(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select((p, i) => new LeaderboardParticipantDto(
            p.Id, p.MerchantId, p.Name, p.Photo, p.VoteCount, i + 1)).ToList();
    }

    // ── Voting ───────────────────────────────────────────────────────────────────

    /// Cast the caller's single vote for this season. One vote per user, final —
    /// re-voting or switching is rejected (matches the app's rule). The vote and
    /// the participant's count update are one transaction.
    public async Task<AwardVoteResult> CastVoteAsync(Guid participantId, CancellationToken ct)
    {
        var voterId = RequireUser();
        var season = await RequireActiveSeasonAsync(ct);

        var config = await _db.Configs.AsNoTracking().FirstOrDefaultAsync(c => c.Season == season, ct);
        if (config is null || !config.VotingOpen)
            throw new AppException("Voting isn't open right now.");

        var participant = await _db.Participants
            .FirstOrDefaultAsync(p => p.Id == participantId && p.Season == season, ct)
            ?? throw AppException.NotFound("That participant isn't in the current award.");
        if (participant.Status != ParticipationStatus.Approved)
            throw new AppException("You can only vote for approved participants.");

        var already = await _db.Votes.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Season == season && v.VoterId == voterId, ct);
        if (already is not null)
            throw new AppException("You've already voted this season and it can't be changed.", 409);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.Votes.Add(new AwardVote { Season = season, VoterId = voterId, ParticipantId = participantId });
        participant.VoteCount += 1;
        participant.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            throw new AppException("You've already voted this season and it can't be changed.", 409);
        }

        return new AwardVoteResult(true, participantId, participant.VoteCount);
    }

    public async Task<MyAwardVoteDto> GetMyVoteAsync(CancellationToken ct)
    {
        var voterId = RequireUser();
        var season = await CurrentSeasonOrNullAsync(ct);
        if (season is null) return new MyAwardVoteDto(null);

        var voted = await _db.Votes.AsNoTracking()
            .Where(v => v.Season == season && v.VoterId == voterId)
            .Select(v => (Guid?)v.ParticipantId)
            .FirstOrDefaultAsync(ct);
        return new MyAwardVoteDto(voted);
    }

    // ── internals ────────────────────────────────────────────────────────────────

    private async Task<string?> CurrentSeasonOrNullAsync(CancellationToken ct)
        => await _db.Configs.AsNoTracking()
            .Where(c => c.IsActive).OrderByDescending(c => c.UpdatedAt)
            .Select(c => c.Season).FirstOrDefaultAsync(ct);

    private async Task<string> RequireActiveSeasonAsync(CancellationToken ct)
        => await CurrentSeasonOrNullAsync(ct)
            ?? throw new AppException("There's no active award right now.");

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    private Guid RequireUser()
        => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw AppException.Unauthorized("You must be signed in.");

    private Guid RequireMerchant()
    {
        var isMerchant = string.Equals(
            Principal?.FindFirstValue(TokenClaims.AccountType), AppRoles.Merchant, StringComparison.OrdinalIgnoreCase);
        if (!isMerchant) throw AppException.Forbidden("Only merchants can participate in awards.");
        return RequireUser();
    }

    private static AwardConfigDto ToDto(AwardConfig c) => new(
        c.Id, c.Season, c.Title, c.IsActive, c.VotingOpen, c.StartsAt, c.EndsAt);

    private static ParticipantDto ToDto(AwardParticipant p) => new(
        p.Id, p.Season, p.MerchantId, p.Name, p.Photo, p.Pitch,
        p.Status.ToWire(), p.VoteCount, p.CreatedAt);
}