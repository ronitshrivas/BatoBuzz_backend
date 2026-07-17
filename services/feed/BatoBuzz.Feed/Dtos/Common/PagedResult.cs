namespace BatoBuzz.Feed.Dtos.Common;

/// Cursor-paginated page. Mirrors the Flutter `FeedPageResult` shape: pass
/// `NextCursor` back as the `cursor` query param to fetch the next page.
/// The cursor is opaque to clients — do not parse it.
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore);
