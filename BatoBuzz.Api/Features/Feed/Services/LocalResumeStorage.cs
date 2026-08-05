using BatoBuzz.Shared.Results;

namespace BatoBuzz.Feed.Services;

/// Saves resumes under wwwroot/uploads/resumes, served as static files. Accepts
/// images and PDFs (the app uploads an image snapshot), capped to protect disk.
public sealed class LocalResumeStorage : IResumeStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalResumeStorage> _log;

    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] Allowed = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    public LocalResumeStorage(IWebHostEnvironment env, ILogger<LocalResumeStorage> log)
        => (_env, _log) = (env, log);

    public async Task<string> SaveAsync(IFormFile file, Guid postId, Guid userId, CancellationToken ct)
    {
        if (file.Length == 0) throw new AppException("The resume file is empty.");
        if (file.Length > MaxBytes)
            throw new AppException($"Resume too large. Maximum size is {MaxBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext)) throw new AppException("Resume must be an image or PDF.");

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
        {
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        var relDir = Path.Combine("uploads", "resumes", postId.ToString());
        var absDir = Path.Combine(webRoot, relDir);
        Directory.CreateDirectory(absDir);

        var name = $"{userId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
        var absPath = Path.Combine(absDir, name);
        await using (var s = new FileStream(absPath, FileMode.Create))
            await file.CopyToAsync(s, ct);

        var url = "/" + Path.Combine(relDir, name).Replace('\\', '/');
        _log.LogInformation("Saved resume {Url} ({Bytes} bytes)", url, file.Length);
        return url;
    }
}