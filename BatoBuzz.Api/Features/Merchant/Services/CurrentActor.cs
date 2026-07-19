using System.Security.Claims;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Merchant.Services;

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

    public bool IsAdmin => _p?.IsInRole(AppRoles.Admin) ?? false;

    public string Phone => _p?.FindFirstValue(ClaimTypes.MobilePhone)
                           ?? _p?.FindFirstValue("phone") ?? string.Empty;

    public string Name => _p?.FindFirstValue(TokenClaims.DisplayName)
                          ?? _p?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
}
