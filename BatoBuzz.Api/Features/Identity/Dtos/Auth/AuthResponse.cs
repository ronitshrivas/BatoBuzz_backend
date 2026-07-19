namespace BatoBuzz.Identity.Dtos.Auth;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    string AccountType,
    object Profile);

public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);