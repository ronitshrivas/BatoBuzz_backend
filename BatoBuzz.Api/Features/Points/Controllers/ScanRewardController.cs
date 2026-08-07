using BatoBuzz.Points.Dtos;
using BatoBuzz.Points.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Points.Controllers;

/// A merchant scanning a customer's BatoBuzz QR to reward them points.
[ApiController]
[Route("api/points/scan")]
[Authorize]
public sealed class ScanRewardController : ControllerBase
{
    private readonly IScanRewardService _scan;
    public ScanRewardController(IScanRewardService scan) => _scan = scan;

    /// Award the scanned customer their points. Returns a status the app maps to
    /// a message ("success", "already", "invalid_code", "user_not_found").
    [HttpPost]
    public async Task<IActionResult> Scan(ScanRewardRequest req, CancellationToken ct)
        => Ok(ApiResponse<ScanRewardResult>.Ok(await _scan.AwardForScanAsync(req.RawValue, ct)));
}