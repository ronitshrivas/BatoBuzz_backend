namespace BatoBuzz.Feed.Dtos.Feed;

/// Result of a toggle: whether the post is now saved, so the UI can flip the
/// heart without a re-fetch.
public sealed record ToggleFavoriteResult(bool IsFavorited);

public sealed record FavoritePostsPage(
    IReadOnlyList<PostDto> Items,
    string? NextCursor,
    bool HasMore);