using BatoBuzz.Shared.Results;

namespace BatoBuzz.Chat.Services;

/// Stores chat media on local disk under wwwroot/uploads/chat, served as static
/// files. Allows the types a chat needs (images, common docs, audio, small
/// video) with a size cap that protects the VM disk.
public sealed class LocalChatMediaStorage : IChatMediaStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalChatMediaStorage> _log;

    private const long MaxBytes = 25 * 1024 * 1024; // 25 MB per attachment
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif",          // images
        ".mp4", ".mov", ".webm",                            // video
        ".m4a", ".aac", ".mp3", ".ogg", ".opus", ".wav",    // audio / voice
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt",   // documents
    };

    public LocalChatMediaStorage(IWebHostEnvironment env, ILogger<LocalChatMediaStorage> log)
        => (_env, _log) = (env, log);

    public async Task<StoredMedia> SaveAsync(IFormFile file, Guid threadId, string kind, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new AppException("The attachment is empty.");
        if (file.Length > MaxBytes)
            throw new AppException($"Attachment too large. Maximum size is {MaxBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext)) throw new AppException("That file type isn't supported in chat.");

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
        {
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        var relDir = Path.Combine("uploads", "chat", threadId.ToString(), kind);
        var absDir = Path.Combine(webRoot, relDir);
        Directory.CreateDirectory(absDir);

        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var safeName = Path.GetFileNameWithoutExtension(file.FileName);
        safeName = string.Concat(safeName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        if (safeName.Length == 0) safeName = "file";
        if (safeName.Length > 60) safeName = safeName[..60];

        var stored = $"{stamp}_{safeName}{ext}";
        var absPath = Path.Combine(absDir, stored);

        await using (var s = new FileStream(absPath, FileMode.Create))
            await file.CopyToAsync(s, ct);

        var url = "/" + Path.Combine(relDir, stored).Replace('\\', '/');
        _log.LogInformation("Saved chat media {Url} ({Bytes} bytes)", url, file.Length);

        var mime = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        return new StoredMedia(url, mime, file.Length, file.FileName);
    }
}