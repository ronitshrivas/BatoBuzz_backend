using BatoBuzz.Merchant.Dtos;
using BatoBuzz.Merchant.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Merchant.Controllers;

/// Admin panel surface — approve/reject merchants and list by status.
[ApiController]
[Route("api/admin/merchants")]
[Authorize(Roles = "admin")]
public sealed class AdminMerchantsController : ControllerBase
{
    private readonly IMerchantService _svc;
    public AdminMerchantsController(IMerchantService svc) => _svc = svc;

    /// `?status=pending` powers the approval queue.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<MerchantProfileDto>>.Ok(await _svc.ListAsync(status, ct)));

    [HttpGet("{merchantId:guid}")]
    public async Task<IActionResult> Get(Guid merchantId, CancellationToken ct)
        => Ok(ApiResponse<MerchantProfileDto>.Ok(await _svc.GetByIdAsync(merchantId, ct)));

    [HttpPost("{merchantId:guid}/review")]
    public async Task<IActionResult> Review(Guid merchantId, ReviewMerchantRequest req, CancellationToken ct)
        => Ok(ApiResponse<MerchantProfileDto>.Ok(await _svc.ReviewAsync(merchantId, req, ct),
            req.Approve ? "Merchant approved." : "Merchant rejected."));
}
