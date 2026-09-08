using System.Security.Claims;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Reservations.Services;

/// The caller, resolved from the JWT minted by Identity. A per-feature copy of
/// the same small helper the Merchant/Feed features use, so Reservations owns
/// its own actor abstraction rather than reaching across features.
public interface ICurrentActor
{
    bool IsAuthenticated { get; }
    Guid Id { get; }             // throws 401 if anonymous
    Guid? IdOrNull { get; }
    bool IsMerchant { get; }
    string Name { get; }
    string Photo { get; }
}

public sealed class CurrentActor : ICurrentActor
{
    private readonly ClaimsPrincipal? _p;
    public CurrentActor(IHttpContextAccessor a) => _p = a.HttpContext?.User;

    public bool IsAuthenticated => _p?.Identity?.IsAuthenticated ?? false;

    public Guid? IdOrNull
    {
        get
        {
            var raw = _p?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public Guid Id => IdOrNull
        ?? throw AppException.Unauthorized("You must be signed in to do that.");

    public bool IsMerchant =>
        string.Equals(_p?.FindFirstValue(TokenClaims.AccountType),
                      AppRoles.Merchant, StringComparison.OrdinalIgnoreCase);

    public string Name => _p?.FindFirstValue(TokenClaims.DisplayName)
                          ?? _p?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public string Photo => _p?.FindFirstValue(TokenClaims.PhotoUrl) ?? string.Empty;
}
