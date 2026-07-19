using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Feed.Services;

public sealed class CityService : ICityService
{
    private readonly FeedDbContext _db;
    public CityService(FeedDbContext db) => _db = db;

    public async Task<IReadOnlyList<CityDto>> GetAllAsync(CancellationToken ct)
    {
        var cities = await _db.Cities
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return cities.Select(c => c.ToDto()).ToList();
    }
}
