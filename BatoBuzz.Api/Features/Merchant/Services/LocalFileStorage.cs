using BatoBuzz.Shared.Results;

namespace BatoBuzz.Merchant.Services;

/// Stores KYC files on the container's disk under wwwroot/uploads, served as
/// static files — the same approach SabMero uses. On the VM this folder is a
/// mounted volume so uploads survive container rebuilds.
///
/// Why not Firebase Storage: it would keep a Firebase dependency in an
/// otherwise-migrated stack. Why not object storage yet: local disk needs no
/// new infra and matches what's already proven in production. Swapping to S3
/// later means implementing this one interface — nothing else changes.
public sealed class LocalFileStorage : IFileStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFileStorage> _log;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxBytes = 8 * 1024 * 1024; // 8 MB — app compresses to ~60% quality already

    public LocalFileStorage(IWebHostEnvironment env, ILogger<LocalFileStorage> log)
        => (_env, _log) = (env, log);

    public async Task<string> SaveAsync(IFormFile file, string subfolder, string fileName, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new AppException("Uploaded file is empty.");
        if (file.Length > MaxBytes)
            throw new AppException("File is too large. Maximum size is 8 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new AppException("Only JPG, PNG or WebP images are allowed.");

        // WebRootPath is null in a container until wwwroot exists — create it
        // so this can't repeat SabMero's null-WebRootPath 500s.
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

        // Forward slashes for the URL regardless of OS path separators.
        var url = "/" + Path.Combine(relDir, safeName).Replace('\\', '/');
        _log.LogInformation("Saved KYC file {Url}", url);
        return url;
    }
}
