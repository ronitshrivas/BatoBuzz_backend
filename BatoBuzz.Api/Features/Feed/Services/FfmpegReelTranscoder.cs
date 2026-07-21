using System.Diagnostics;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Feed.Services;

/// Transcodes reels to HLS with FFmpeg on the local box.
///
/// IMPORTANT (single small VM): FFmpeg is CPU-heavy. This runs ONLY from the
/// background worker (never on the request thread), is capped to one thread,
/// and uses the ultrafast preset to keep a single reel from pinning both
/// cores for long. It produces a single 720p rendition — not a full adaptive
/// ladder — because multi-rendition encoding would multiply the CPU cost. If
/// reels grow, move this to a transcoding service; callers won't change.
public sealed class FfmpegReelTranscoder : IReelTranscoder
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FfmpegReelTranscoder> _log;

    public FfmpegReelTranscoder(IWebHostEnvironment env, ILogger<FfmpegReelTranscoder> log)
        => (_env, _log) = (env, log);

    public async Task<TranscodeResult> TranscodeAsync(string sourcePath, string relFolder, CancellationToken ct)
    {
        var webRoot = _env.WebRootPath
            ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var absDir = Path.Combine(webRoot, "uploads", relFolder);
        Directory.CreateDirectory(absDir);

        var playlistName = "index.m3u8";
        var absPlaylist = Path.Combine(absDir, playlistName);
        var absThumb = Path.Combine(absDir, "thumb.jpg");

        // ── HLS: single 720p rendition, ultrafast, 1 thread, 4s segments ──────
        // -vf scale caps height at 720 keeping aspect; -threads 1 protects cores.
        var hlsArgs =
            $"-y -i \"{sourcePath}\" " +
            "-threads 1 -preset ultrafast " +
            "-vf \"scale=-2:min(720\\,ih)\" " +
            "-c:v libx264 -profile:v main -crf 26 -maxrate 2M -bufsize 4M " +
            "-c:a aac -b:a 128k -ac 2 " +
            "-hls_time 4 -hls_playlist_type vod " +
            $"-hls_segment_filename \"{Path.Combine(absDir, "seg_%03d.ts")}\" " +
            $"\"{absPlaylist}\"";

        await RunFfmpegAsync(hlsArgs, ct);

        // ── Thumbnail: one frame at ~1s ──────────────────────────────────────
        var thumbArgs = $"-y -ss 00:00:01 -i \"{sourcePath}\" -frames:v 1 -vf \"scale=-2:720\" \"{absThumb}\"";
        try { await RunFfmpegAsync(thumbArgs, ct); }
        catch (Exception ex)
        {
            // A missing thumbnail shouldn't fail the whole reel.
            _log.LogWarning(ex, "Thumbnail extraction failed for {Folder}", relFolder);
        }

        var baseUrl = "/uploads/" + relFolder.Replace('\\', '/');
        return new TranscodeResult($"{baseUrl}/{playlistName}", $"{baseUrl}/thumb.jpg");
    }

    private async Task RunFfmpegAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = new Process { StartInfo = psi };
        try { proc.Start(); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to start ffmpeg");
            throw new AppException("Video processing is unavailable (ffmpeg not found).");
        }

        // Drain stderr so the process can't block on a full pipe.
        var stderrTask = proc.StandardError.ReadToEndAsync();

        // Hard timeout so a pathological file can't hold a core forever.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await proc.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            throw new AppException("Video processing timed out.");
        }

        var stderr = await stderrTask;
        if (proc.ExitCode != 0)
        {
            _log.LogError("ffmpeg failed ({Code}): {Err}", proc.ExitCode,
                stderr.Length > 800 ? stderr[^800..] : stderr);
            throw new AppException("Video processing failed.");
        }
    }
}