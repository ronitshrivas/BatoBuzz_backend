using BatoBuzz.Identity.Dtos.Auth;
using BatoBuzz.Identity.Dtos.Merchant;

namespace BatoBuzz.Identity.Services;

public interface IMerchantAuthService
{
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct);
    Task<AuthResponse> SignupAsync(MerchantSignupRequest req, CancellationToken ct);
    Task<AuthResponse> LoginAsync(MerchantLoginRequest req, CancellationToken ct);
}