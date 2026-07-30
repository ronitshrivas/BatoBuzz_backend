using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Feed.Controllers;

/// Saving posts. All endpoints act as the signed-in user.
[ApiController]
[Route("api/feed/favorites")]
[Authorize]
public sealed class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favorites;
    public FavoritesController(IFavoriteService favorites) => _favorites = favorites;

    /// Toggle save on a post. Returns whether it's now saved.
    [HttpPost("{postId:guid}")]
    public async Task<IActionResult> Toggle(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<ToggleFavoriteResult>.Ok(await _favorites.ToggleAsync(postId, ct)));

    /// The ids of every post the user has saved — for heart icons across feeds.
    [HttpGet("ids")]
    public async Task<IActionResult> Ids(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<Guid>>.Ok(await _favorites.GetFavoriteIdsAsync(ct)));

    /// The saved posts themselves, most recently saved first.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? cursor,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<FavoritePostsPage>.Ok(await _favorites.GetFavoritePostsAsync(cursor, pageSize, ct)));
}