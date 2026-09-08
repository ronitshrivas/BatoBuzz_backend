using System.Security.Claims;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.PostInterest.Services;

/// The caller, resolved from the JWT. Per-feature copy so PostInterest owns its actor.
public interface ICurrentActor
{
    Guid Id { get; }             // throws 401 if anonymous
    Guid? IdOrNull { get; }
}

public sealed class CurrentActor : ICurrentActor
{
    private readonly ClaimsPrincipal? _p;
    public CurrentActor(IHttpContextAccessor a) => _p = a.HttpContext?.User;

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
}
