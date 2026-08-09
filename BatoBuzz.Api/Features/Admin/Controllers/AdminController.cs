using BatoBuzz.Admin.Dtos;
using BatoBuzz.Admin.Services;
using BatoBuzz.Merchant.Dtos;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Admin.Controllers;

/// Super-admin surface: dashboard, user + merchant management. Locked to the
/// superadmin role — a level above ordinary admins (who can still do the
/// existing per-feature admin tasks).
[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "superadmin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminManagementService _svc;
    public AdminController(IAdminManagementService svc) => _svc = svc;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
        => Ok(ApiResponse<DashboardStatsDto>.Ok(await _svc.GetDashboardAsync(ct)));

    // ── Users ────────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] bool includeDeleted = false,
        [FromQuery] string? cursor = null, [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => Ok(ApiResponse<AdminUserListPage>.Ok(await _svc.ListUsersAsync(search, includeDeleted, cursor, pageSize, ct)));

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> User(Guid id, CancellationToken ct)
        => Ok(ApiResponse<AdminUserDto>.Ok(await _svc.GetUserAsync(id, ct)));

    [HttpPost("users/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid id, SuspendRequest req, CancellationToken ct)
        => Ok(ApiResponse<AdminUserDto>.Ok(await _svc.SetUserSuspendedAsync(id, req.Suspend, req.Note, ct)));

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        await _svc.DeleteUserAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null, "User deleted."));
    }

    // ── Merchants ──────────────────────────────────────────────────────────────

    [HttpGet("merchants")]
    public async Task<IActionResult> Merchants([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] bool includeDeleted = false, [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => Ok(ApiResponse<AdminMerchantListPage>.Ok(await _svc.ListMerchantsAsync(search, status, includeDeleted, cursor, pageSize, ct)));

    [HttpGet("merchants/{id:guid}")]
    public async Task<IActionResult> Merchant(Guid id, CancellationToken ct)
        => Ok(ApiResponse<AdminMerchantDto>.Ok(await _svc.GetMerchantAsync(id, ct)));

    [HttpPost("merchants/{id:guid}/review")]
    public async Task<IActionResult> ReviewMerchant(Guid id, BatoBuzz.Admin.Dtos.ReviewMerchantRequest req, CancellationToken ct)
        => Ok(ApiResponse<AdminMerchantDto>.Ok(await _svc.ReviewMerchantAsync(id, req.Approve, req.Note, ct)));

    [HttpPost("merchants/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendMerchant(Guid id, SuspendRequest req, CancellationToken ct)
        => Ok(ApiResponse<AdminMerchantDto>.Ok(await _svc.SetMerchantSuspendedAsync(id, req.Suspend, req.Note, ct)));

    [HttpDelete("merchants/{id:guid}")]
    public async Task<IActionResult> DeleteMerchant(Guid id, CancellationToken ct)
    {
        await _svc.DeleteMerchantAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null, "Merchant deleted."));
    }
}