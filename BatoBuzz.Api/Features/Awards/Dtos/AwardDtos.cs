using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Awards.Dtos;

public sealed record AwardConfigDto(
    Guid Id, string Season, string Title, bool IsActive, bool VotingOpen,
    DateTime? StartsAt, DateTime? EndsAt);

public sealed record ParticipantDto(
    Guid Id, string Season, Guid MerchantId, string Name, string? Photo, string? Pitch,
    string Status, int VoteCount, DateTime CreatedAt);

public sealed record LeaderboardParticipantDto(
    Guid Id, Guid MerchantId, string Name, string? Photo, int VoteCount, int Rank);

/// Merchant applies to the current award season.
public sealed record SubmitParticipationRequest(
    [Required, MaxLength(200)] string Name,
    string? Photo,
    [MaxLength(1000)] string? Pitch);

/// Admin approves/rejects a participant.
public sealed record ReviewParticipantRequest([Required] bool Approve);

/// User casts their award vote for a participant.
public sealed record CastAwardVoteRequest([Required] Guid ParticipantId);

public sealed record MyAwardVoteDto(Guid? ParticipantId);

public sealed record AwardVoteResult(bool Success, Guid ParticipantId, int ParticipantVoteCount);

/// Admin sets/updates the active award config.
public sealed record UpsertConfigRequest(
    [Required, MaxLength(64)] string Season,
    [Required, MaxLength(200)] string Title,
    bool IsActive,
    bool VotingOpen,
    DateTime? StartsAt,
    DateTime? EndsAt);