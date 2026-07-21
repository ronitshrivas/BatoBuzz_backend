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

/// Transcode lifecycle for a reel's video. None = not a reel / no video yet;
/// Processing = raw MP4 saved, HLS being generated in the background;
/// Ready = .m3u8 available; Failed = transcode errored (raw MP4 still playable).
public enum ReelStatus { None = 0, Processing = 1, Ready = 2, Failed = 3 }