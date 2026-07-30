using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Merchant.Dtos;

// ── Ratings ────────────────────────────────────────────────────────────────

public sealed record SubmitRatingRequest([Range(1, 5)] int Rating);

/// A merchant's aggregate rating plus the caller's own rating (null if they
/// haven't rated), so the UI can show the average and highlight their stars.
public sealed record MerchantRatingSummaryDto(
    Guid MerchantId,
    double Average,
    int Count,
    int? MyRating);

// ── Award voting ─────────────────────────────────────────────────────────────

public sealed record CastVoteRequest([Required] Guid MerchantId);

/// The caller's vote state: who they voted for (null if they haven't voted).
/// Because a vote is one-per-user and final, this is all the UI needs.
public sealed record MyVoteDto(Guid? VotedMerchantId);

public sealed record VoteResult(bool Success, Guid VotedMerchantId, int MerchantVoteCount);

public sealed record MerchantVoteCountDto(Guid MerchantId, int VoteCount);