using BatoBuzz.Identity.Dtos.Auth;
using BatoBuzz.Identity.Dtos.User;
using BatoBuzz.Identity.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Identity.Controllers;

[ApiController]
[Route("api/user/auth")]
public sealed class UserAuthController : ControllerBase
{
    private readonly IUserAuthService _auth;
    private readonly IRefreshService _refresh;

    public UserAuthController(IUserAuthService auth, IRefreshService refresh)
        => (_auth, _refresh) = (auth, refresh);

    [HttpPost("register")]
    public async Task<IActionResult> Register(UserRegisterRequest req, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.RegisterAsync(req, ct)));

    [HttpPost("login")]
    public async Task<IActionResult> Login(UserLoginRequest req, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.LoginAsync(req, ct)));

    [HttpPost("google")]
    public async Task<IActionResult> Google(GoogleLoginRequest req, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.GoogleLoginAsync(req, ct)));

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