using BatoBuzz.Reservations.Dtos;
using BatoBuzz.Reservations.Services;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Reservations.Controllers;

/// The merchant side of grab / reserve. Replaces the Firestore-callable
/// functions completeReservation / cancelReservationByMerchant / noShowReservation
/// and the merchant's `reservations` reads. Approved merchants only.
[ApiController]
[Route("api/merchant/reservations")]
[Authorize(Policy = AppPolicies.ApprovedMerchant)]
public sealed class MerchantReservationsController : ControllerBase
{
    private readonly IReservationService _svc;
    public MerchantReservationsController(IReservationService svc) => _svc = svc;

    /// Reservations placed with this merchant, newest first (bounded page).
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ReservationDto>>.Ok(await _svc.GetMerchantReservationsAsync(ct)));

    /// Complete a pickup. Pass UserId (decoded from the customer's QR) for the
    /// scan path, or omit it for an in-person "mark arrived". Idempotent.
    [HttpPost("{reservationId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid reservationId, CompleteReservationRequest req, CancellationToken ct)
        => Ok(ApiResponse<ReservationActionResultDto>.Ok(await _svc.CompleteAsync(reservationId, req.UserId, ct)));

    /// Cancel an active reservation; the held stock is released.
    [HttpPost("{reservationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid reservationId, CancellationToken ct)
        => Ok(ApiResponse<GrabResultDto>.Ok(await _svc.CancelByMerchantAsync(reservationId, ct)));

    /// Mark an expired, uncollected reservation as a no-show; the customer's
    /// points are deducted and (optionally) they're blacklisted from this merchant.
    [HttpPost("{reservationId:guid}/no-show")]
    public async Task<IActionResult> NoShow(Guid reservationId, NoShowRequest req, CancellationToken ct)
        => Ok(ApiResponse<ReservationActionResultDto>.Ok(
            await _svc.NoShowAsync(reservationId, req.Blacklist, req.Reason, ct)));
}
