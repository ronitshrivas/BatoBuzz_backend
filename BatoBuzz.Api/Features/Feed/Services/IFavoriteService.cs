using BatoBuzz.Feed.Dtos.Feed;

namespace BatoBuzz.Feed.Services;

public interface IFavoriteService
{
    Task<ToggleFavoriteResult> ToggleAsync(Guid postId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetFavoriteIdsAsync(CancellationToken ct);
    Task<FavoritePostsPage> GetFavoritePostsAsync(string? cursor, int pageSize, CancellationToken ct);
}