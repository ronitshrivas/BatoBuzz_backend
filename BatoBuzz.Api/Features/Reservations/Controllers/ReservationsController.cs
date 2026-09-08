using BatoBuzz.Reservations.Dtos;
using BatoBuzz.Reservations.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Reservations.Controllers;

/// The customer side of grab / reserve. Replaces the Firestore-callable
/// functions grabReservation / cancelReservationByUser / updateReservationQuantity
/// and the client-side `reservations` reads.
[ApiController]
[Route("api/reservations")]
[Authorize]
public sealed class ReservationsController : ControllerBase
{
    private readonly IReservationService _svc;
    public ReservationsController(IReservationService svc) => _svc = svc;

    /// Grab one or more units for an 8-hour hold.
    [HttpPost("grab")]
    public async Task<IActionResult> Grab(GrabRequest req, CancellationToken ct)
        => Ok(ApiResponse<GrabResultDto>.Ok(await _svc.GrabAsync(req, ct)));

    /// Cancel your own hold (only within the first hour).
    [HttpPost("{reservationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid reservationId, CancellationToken ct)
        => Ok(ApiResponse<GrabResultDto>.Ok(await _svc.CancelByUserAsync(reservationId, ct)));

    /// Change the quantity of an active hold; 0 releases it.
    [HttpPut("{reservationId:guid}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid reservationId, UpdateQuantityRequest req, CancellationToken ct)
        => Ok(ApiResponse<GrabResultDto>.Ok(await _svc.UpdateQuantityAsync(reservationId, req.Quantity, ct)));

    /// Your reservations, newest first.
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ReservationDto>>.Ok(await _svc.GetMyReservationsAsync(ct)));

    /// How many active holds you currently have (for the 3-max hint).
    [HttpGet("mine/active-count")]
    public async Task<IActionResult> ActiveCount(CancellationToken ct)
        => Ok(ApiResponse<int>.Ok(await _svc.GetMyActiveCountAsync(ct)));

    /// Your active hold on a given post, or null.
    [HttpGet("posts/{postId:guid}/active-hold")]
    public async Task<IActionResult> ActiveHold(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<ReservationDto?>.Ok(await _svc.GetActiveHoldAsync(postId, ct)));

    /// Live reservable stock for a post (null if not tracked yet).
    [HttpGet("posts/{postId:guid}/stock")]
    public async Task<IActionResult> Stock(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<int?>.Ok(await _svc.GetStockAsync(postId, ct)));

    /// Whether a merchant has restricted you from reserving.
    [HttpGet("merchants/{merchantId:guid}/block-status")]
    public async Task<IActionResult> BlockStatus(Guid merchantId, CancellationToken ct)
        => Ok(ApiResponse<BlockStatusDto>.Ok(await _svc.GetBlockStatusAsync(merchantId, ct)));
}
