using BatoBuzz.Identity.Dtos.Auth;
using BatoBuzz.Identity.Dtos.Merchant;
using BatoBuzz.Identity.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Identity.Controllers;

[ApiController]
[Route("api/merchant/auth")]
public sealed class MerchantAuthController : ControllerBase
{
    private readonly IMerchantAuthService _auth;
    private readonly IRefreshService _refresh;

    public MerchantAuthController(IMerchantAuthService auth, IRefreshService refresh)
        => (_auth, _refresh) = (auth, refresh);

    [HttpGet("phone-exists")]
    public async Task<IActionResult> PhoneExists([FromQuery] string phone, CancellationToken ct)
        => Ok(ApiResponse<PhoneExistsResponse>.Ok(
            new PhoneExistsResponse(await _auth.PhoneExistsAsync(phone, ct))));

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(MerchantSignupRequest req, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.SignupAsync(req, ct)));

    [HttpPost("login")]
    public async Task<IActionResult> Login(MerchantLoginRequest req, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.LoginAsync(req, ct)));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _refresh.RefreshAsync(req.RefreshToken, ct)));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest req, CancellationToken ct)
    {
        await _refresh.RevokeAsync(req.RefreshToken, ct);
        return Ok(ApiResponse<string>.Ok("ok", "Logged out."));
    }
}