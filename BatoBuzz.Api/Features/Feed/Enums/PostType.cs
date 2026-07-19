namespace BatoBuzz.Feed.Enums;

/// Mirrors the `postType` string stored on Firestore MerchantPost docs.
/// Sent over the wire as the same lowercase strings the Flutter apps already
/// use: "ads", "reels", "job", "event".
public enum PostType
{
    Ads = 0,
    Reels = 1,
    Job = 2,
    Event = 3,
}
