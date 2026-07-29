using System.Security.Claims;

namespace BatoBuzz.Chat.Services;

/// Verifies a caller participates in a thread. Used by the hub before letting a
/// connection join a thread's group.
public interface IThreadAccessGuard
{
    Task<bool> CanAccessAsync(Guid threadId, ClaimsPrincipal? user);
}