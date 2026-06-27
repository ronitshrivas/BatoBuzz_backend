namespace BatoBuzz.Identity.Services;

public sealed record GoogleUserInfo(string Subject, string Email, string? Name, string? Picture);

public interface IGoogleAuthValidator
{
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default);
}