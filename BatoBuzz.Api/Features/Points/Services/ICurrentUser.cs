using System.Security.Claims;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Points.Services;

/// The signed-in user, from the JWT. Points always belong to the caller —
/// never a user id supplied in the request body — so nobody can award
/// themselves points on someone else's behalf.
public interface ICurrentUser
{
    Guid Id { get; }
    Guid? IdOrNull { get; }
}

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _p;
    public CurrentUser(IHttpContextAccessor a) => _p = a.HttpContext?.User;

    public Guid? IdOrNull =>
        Guid.TryParse(_p?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid Id => IdOrNull
        ?? throw AppException.Unauthorized("You must be signed in to do that.");
}