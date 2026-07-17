using System.Security.Claims;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Feed.Services;

/// Reads the caller out of the JWT.
///
/// Name and photo are taken from token claims rather than fetched from Identity
/// on every write: the feed denormalizes author name/photo onto each post and
/// comment (exactly as the Firestore documents did), so a per-write HTTP hop to
/// Identity would buy nothing but latency and a failure mode.
public sealed class CurrentActor : ICurrentActor
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentActor(IHttpContextAccessor accessor)
        => _principal = accessor.HttpContext?.User;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid? IdOrNull
    {
        get
        {
            var raw = _principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public Guid Id => IdOrNull
        ?? throw AppException.Unauthorized("You must be signed in to do that.");

    public AuthorType Type =>
        string.Equals(_principal?.FindFirstValue(TokenClaims.AccountType),
                      AppRoles.Merchant, StringComparison.OrdinalIgnoreCase)
            ? AuthorType.Merchant
            : AuthorType.User;

    public bool IsMerchant => Type == AuthorType.Merchant;

    public string Name =>
        _principal?.FindFirstValue(TokenClaims.DisplayName)
        ?? _principal?.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    public string Photo =>
        _principal?.FindFirstValue(TokenClaims.PhotoUrl) ?? string.Empty;
}
