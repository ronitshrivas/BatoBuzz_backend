namespace BatoBuzz.Feed.Dtos.Feed;

/// Result of uploading a reel's video (+ optional thumbnail) to storage. The app
/// takes these URLs and includes them when it creates the reel post, so upload
/// and post-creation stay two clean steps (mirrors how the Firebase flow first
/// uploaded to Storage, then wrote the doc).
public sealed record ReelUploadResult(string ReelVideoUrl, string? ReelThumbnailUrl);