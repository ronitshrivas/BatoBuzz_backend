using System.Security.Claims;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Chat.Services;

/// The signed-in chat participant — either a user or a merchant. Identity comes
/// from the JWT, never the request, so nobody can post as someone else.
public interface IChatActor
{
    Guid Id { get; }
    bool IsMerchant { get; }
    string Name { get; }
}

public sealed class ChatActor : IChatActor
{
    private readonly ClaimsPrincipal? _p;
    public ChatActor(IHttpContextAccessor a) => _p = a.HttpContext?.User;

    public Guid Id =>
        Guid.TryParse(_p?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw AppException.Unauthorized("You must be signed in to chat.");

    public bool IsMerchant =>
        string.Equals(_p?.FindFirstValue(TokenClaims.AccountType), AppRoles.Merchant,
            StringComparison.OrdinalIgnoreCase);

    public string Name => _p?.FindFirstValue(TokenClaims.DisplayName) ?? "";
}