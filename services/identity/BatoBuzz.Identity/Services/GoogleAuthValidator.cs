using BatoBuzz.Shared.Results;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace BatoBuzz.Identity.Services;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";
    public string[] ClientIds { get; init; } = Array.Empty<string>();
}

public sealed class GoogleAuthValidator : IGoogleAuthValidator
{
    private readonly GoogleAuthOptions _opt;
    public GoogleAuthValidator(IOptions<GoogleAuthOptions> opt) => _opt = opt.Value;

    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _opt.ClientIds   // your Android/iOS/Web OAuth client IDs
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleUserInfo(payload.Subject, payload.Email, payload.Name, payload.Picture);
        }
        catch (InvalidJwtException)
        {
            throw AppException.Unauthorized("Invalid Google token.");
        }
    }
}