using BatoBuzz.ServiceProvider.Dtos;
using BatoBuzz.ServiceProvider.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.ServiceProvider.Controllers;

/// User-facing: apply, check status, browse approved providers, review them.
[ApiController]
[Route("api/service-providers")]
public sealed class ServiceProvidersController : ControllerBase
{
    private readonly IServiceProviderService _svc;
    public ServiceProvidersController(IServiceProviderService svc) => _svc = svc;

    /// Registration: multipart/form-data with fields + optional photo/document.
    [HttpPost("apply")]
    [Authorize]
    [RequestSizeLimit(40 * 1024 * 1024)]
    public async Task<IActionResult> Apply([FromForm] SubmitApplicationRequest req, CancellationToken ct)
        => Ok(ApiResponse<ProviderDto>.Ok(await _svc.SubmitAsync(req, ct), "Application submitted for review."));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => Ok(ApiResponse<ProviderDto>.Ok(await _svc.GetMineAsync(ct)));

    /// The login gate polls this on startup.
    [HttpGet("me/status")]
    [Authorize]
    public async Task<IActionResult> MyStatus(CancellationToken ct)
        => Ok(ApiResponse<ProviderStatusDto>.Ok(await _svc.GetMyStatusAsync(ct)));

    /// The Local Services list — approved providers. Anonymous-readable.
    /// `?category=Plumbing&search=text`
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] string? category, [FromQuery] string? search, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProviderDto>>.Ok(await _svc.ListApprovedAsync(category, search, ct)));

    [HttpGet("{providerId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid providerId, CancellationToken ct)
        => Ok(ApiResponse<ProviderDto>.Ok(await _svc.GetByIdAsync(providerId, ct)));

    // ── Reviews ──────────────────────────────────────────────────────────────

    [HttpGet("{providerId:guid}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> Reviews(Guid providerId, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ReviewDto>>.Ok(await _svc.GetReviewsAsync(providerId, ct)));

    /// Add or update the caller's review (one per provider).
    [HttpPut("{providerId:guid}/reviews")]
    [Authorize]
    public async Task<IActionResult> UpsertReview(Guid providerId, UpsertReviewRequest req, CancellationToken ct)
        => Ok(ApiResponse<ReviewDto>.Ok(await _svc.UpsertReviewAsync(providerId, req, ct), "Review saved."));

    [HttpDelete("{providerId:guid}/reviews")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(Guid providerId, CancellationToken ct)
    {
        await _svc.DeleteReviewAsync(providerId, ct);
        return Ok(ApiResponse<string>.Ok("ok", "Review removed."));
    }
}