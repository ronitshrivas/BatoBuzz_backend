// Services/IRefreshService.cs
using BatoBuzz.Identity.Dtos.Auth;
namespace BatoBuzz.Identity.Services;

public interface IRefreshService
{
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct);
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}