using BatoBuzz.ServiceProvider.Dtos;
using BatoBuzz.ServiceProvider.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.ServiceProvider.Controllers;

/// Admin: approval queue + approve/reject.
[ApiController]
[Route("api/admin/service-providers")]
[Authorize(Roles = "admin")]
public sealed class AdminProvidersController : ControllerBase
{
    private readonly IServiceProviderService _svc;
    public AdminProvidersController(IServiceProviderService svc) => _svc = svc;

    /// `?status=pending` powers the queue.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProviderDto>>.Ok(await _svc.AdminListAsync(status, ct)));

    [HttpPost("{providerId:guid}/review")]
    public async Task<IActionResult> Review(Guid providerId, ReviewProviderRequest req, CancellationToken ct)
        => Ok(ApiResponse<ProviderDto>.Ok(await _svc.ReviewApplicationAsync(providerId, req, ct),
            req.Approve ? "Application approved." : "Application rejected."));
}