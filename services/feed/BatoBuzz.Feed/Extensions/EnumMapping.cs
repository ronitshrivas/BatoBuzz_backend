using BatoBuzz.Feed.Enums;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Feed.Extensions;

/// The Flutter apps speak lowercase strings ("ads", "latest", "user"). Keeping
/// the translation in one place means the wire contract stays stable even if
/// the enum members are ever renamed or reordered.
public static class EnumMapping
{
    public static string ToWire(this PostType value) => value switch
    {
        PostType.Ads => "ads",
        PostType.Reels => "reels",
        PostType.Job => "job",
        PostType.Event => "event",
        _ => "ads",
    };

    public static PostType ToPostType(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "ads" => PostType.Ads,
        "reels" => PostType.Reels,
        "job" => PostType.Job,
        "event" => PostType.Event,
        _ => throw new AppException("Unknown post type. Expected one of: ads, reels, job, event."),
    };

    public static PostType? ToPostTypeOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ToPostType(value);

    public static FeedSort ToFeedSort(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" or "latest" => FeedSort.Latest,
        "oldest" => FeedSort.Oldest,
        "mostviewed" => FeedSort.MostViewed,
        "foryou" => FeedSort.ForYou,
        _ => throw new AppException("Unknown sort. Expected one of: latest, oldest, mostViewed, forYou."),
    };

    public static string ToWire(this AuthorType value) =>
        value == AuthorType.Merchant ? "merchant" : "user";
}
