using BatoBuzz.Merchant.Dtos;
using BatoBuzz.Merchant.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Merchant.Controllers;

/// Star ratings and the single award vote for merchants.
[ApiController]
[Route("api/merchants")]
public sealed class MerchantRatingVoteController : ControllerBase
{
    private readonly IRatingVoteService _svc;
    public MerchantRatingVoteController(IRatingVoteService svc) => _svc = svc;

    // ── Ratings ──────────────────────────────────────────────────────────────

    /// The merchant's average rating + count (and the caller's own if signed in).
    /// Anonymous-readable so ratings show on public profiles.
    [HttpGet("{merchantId:guid}/rating")]
    [AllowAnonymous]
    public async Task<IActionResult> Rating(Guid merchantId, CancellationToken ct)
        => Ok(ApiResponse<MerchantRatingSummaryDto>.Ok(await _svc.GetRatingSummaryAsync(merchantId, ct)));

    /// Submit or update the caller's 1-5 rating.
    [HttpPost("{merchantId:guid}/rating")]
    [Authorize]
    public async Task<IActionResult> SubmitRating(Guid merchantId, SubmitRatingRequest req, CancellationToken ct)
        => Ok(ApiResponse<MerchantRatingSummaryDto>.Ok(await _svc.SubmitRatingAsync(merchantId, req.Rating, ct)));

    // ── Award voting ─────────────────────────────────────────────────────────

    /// The merchant's total award votes. Anonymous-readable.
    [HttpGet("{merchantId:guid}/votes")]
    [AllowAnonymous]
    public async Task<IActionResult> VoteCount(Guid merchantId, CancellationToken ct)
        => Ok(ApiResponse<MerchantVoteCountDto>.Ok(await _svc.GetVoteCountAsync(merchantId, ct)));

    /// Who the caller voted for (null if they haven't voted).
    [HttpGet("votes/mine")]
    [Authorize]
    public async Task<IActionResult> MyVote(CancellationToken ct)
        => Ok(ApiResponse<MyVoteDto>.Ok(await _svc.GetMyVoteAsync(ct)));

    /// Cast the caller's one-and-only award vote.
    [HttpPost("votes")]
    [Authorize]
    public async Task<IActionResult> CastVote(CastVoteRequest req, CancellationToken ct)
        => Ok(ApiResponse<VoteResult>.Ok(await _svc.CastVoteAsync(req.MerchantId, ct), "Vote cast."));
}