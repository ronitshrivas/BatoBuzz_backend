using BatoBuzz.Merchant.Dtos;
using BatoBuzz.Merchant.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Merchant.Controllers;

/// The merchant's own profile + KYC. Every route requires a merchant token.
/// This is registration step 2 — the auth record already exists in Identity.
[ApiController]
[Route("api/merchant/profile")]
[Authorize(Roles = "merchant")]
public sealed class MerchantProfileController : ControllerBase
{
    private readonly IMerchantService _svc;
    public MerchantProfileController(IMerchantService svc) => _svc = svc;

    /// Registration: multipart/form-data with the fields + up to four KYC files.
    /// RequestSizeLimit covers four compressed images comfortably.
    [HttpPost]
    [RequestSizeLimit(40 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateMerchantProfileRequest req, CancellationToken ct)
        => Ok(ApiResponse<MerchantProfileDto>.Ok(await _svc.CreateAsync(req, ct), "Profile submitted for review."));

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Ok(ApiResponse<MerchantProfileDto>.Ok(await _svc.GetMineAsync(ct)));

    /// The app's login gate polls this on startup — cheap, one row, one column.
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
        => Ok(ApiResponse<MerchantStatusDto>.Ok(await _svc.GetMyStatusAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateMerchantProfileRequest req, CancellationToken ct)
        => Ok(ApiResponse<MerchantProfileDto>.Ok(await _svc.UpdateAsync(req, ct), "Profile updated."));
}
