using BatoBuzz.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Points.Services;

/// Reads display names/photos from the Identity database for leaderboard rows.
///
/// One batched query for all ids on the page (not one per row), which is the
/// fix for the N+1 the Firebase version had — it fetched each user document
/// individually inside the leaderboard loop.
public sealed class IdentityUserDirectory : IUserDirectory
{
    private readonly IdentityDbContext _db;
    public IdentityUserDirectory(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, UserProfileBrief>> GetProfilesAsync(
        IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, UserProfileBrief>();

        var rows = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.PhotoUrl })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => new UserProfileBrief(r.DisplayName, r.PhotoUrl));
    }
}