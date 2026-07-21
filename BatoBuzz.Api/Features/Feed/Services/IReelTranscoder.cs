namespace BatoBuzz.Feed.Services;

/// Result of transcoding a raw reel video: the HLS playlist URL and a poster
/// thumbnail URL, both as public /uploads paths.
public sealed record TranscodeResult(string HlsUrl, string ThumbnailUrl);

/// Wraps FFmpeg. Kept behind an interface so a future move to a managed service
/// (Mux, Cloudflare Stream) replaces just this implementation.
public interface IReelTranscoder
{
    /// Transcodes the source file at absolute path `sourcePath` into an HLS
    /// playlist + segments under the reel's folder, and extracts a thumbnail.
    Task<TranscodeResult> TranscodeAsync(string sourcePath, string relFolder, CancellationToken ct);
}