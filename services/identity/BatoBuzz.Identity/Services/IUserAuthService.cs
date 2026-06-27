using BatoBuzz.Identity.Dtos.Auth;
using BatoBuzz.Identity.Dtos.User;

namespace BatoBuzz.Identity.Services;

public interface IUserAuthService
{
    Task<AuthResponse> RegisterAsync(UserRegisterRequest req, CancellationToken ct);
    Task<AuthResponse> LoginAsync(UserLoginRequest req, CancellationToken ct);
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest req, CancellationToken ct);
}