using BatoBuzz.Shared.Results;

namespace BatoBuzz.Feed.Services;

/// Stores reel videos under wwwroot/uploads/reels, served as static files —
/// the same disk approach used for images, with video-appropriate limits.
///
/// NOTE on scale: video on a single VM's disk works for launch, but streaming
/// many reels through one server is bandwidth- and space-limited. When reels
/// see real traffic, implement IVideoStorage against object storage; callers
/// won't change.
public sealed class LocalVideoStorage : IVideoStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalVideoStorage> _log;

    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".webm" };
    private static readonly string[] ThumbExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxVideoBytes = 60 * 1024 * 1024; // 60 MB — a short reel
    private const long MaxThumbBytes = 4 * 1024 * 1024;

    public LocalVideoStorage(IWebHostEnvironment env, ILogger<LocalVideoStorage> log)
        => (_env, _log) = (env, log);

    public Task<string> SaveVideoAsync(IFormFile file, string subfolder, string fileName, CancellationToken ct)
        => SaveAsync(file, subfolder, fileName, VideoExtensions, MaxVideoBytes,
                     "Only MP4, MOV or WebM videos are allowed.", ct);

    public Task<string> SaveThumbnailAsync(IFormFile file, string subfolder, string fileName, CancellationToken ct)
        => SaveAsync(file, subfolder, fileName, ThumbExtensions, MaxThumbBytes,
                     "Only JPG, PNG or WebP thumbnails are allowed.", ct);

    private async Task<string> SaveAsync(
        IFormFile file, string subfolder, string fileName,
        string[] allowed, long maxBytes, string typeError, CancellationToken ct)
    {
        if (file.Length == 0) throw new AppException("Uploaded file is empty.");
        if (file.Length > maxBytes)
            throw new AppException($"File is too large. Maximum size is {maxBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) throw new AppException(typeError);

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
        {
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        var relDir = Path.Combine("uploads", subfolder);
        var absDir = Path.Combine(webRoot, relDir);
        Directory.CreateDirectory(absDir);

        var safeName = $"{fileName}{ext}";
        var absPath = Path.Combine(absDir, safeName);

        await using (var stream = new FileStream(absPath, FileMode.Create))
            await file.CopyToAsync(stream, ct);

        var url = "/" + Path.Combine(relDir, safeName).Replace('\\', '/');
        _log.LogInformation("Saved reel media {Url} ({Bytes} bytes)", url, file.Length);
        return url;
    }
}