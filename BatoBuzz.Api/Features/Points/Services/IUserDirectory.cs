namespace BatoBuzz.Points.Services;

public readonly record struct UserProfileBrief(string DisplayName, string? PhotoUrl);

/// Looks up display names/photos for leaderboard rows.
///
/// Points live in their own database, so this crosses a feature boundary to
/// Identity. It's an interface (not a direct DbContext reference) to keep that
/// dependency explicit and one-directional — Points reads Identity, never the
/// other way around.
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserProfileBrief>> GetProfilesAsync(
        IEnumerable<Guid> userIds, CancellationToken ct);
}