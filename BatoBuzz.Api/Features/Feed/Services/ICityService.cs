using BatoBuzz.Feed.Dtos.Feed;

namespace BatoBuzz.Feed.Services;

public interface ICityService
{
    Task<IReadOnlyList<CityDto>> GetAllAsync(CancellationToken ct);
}
