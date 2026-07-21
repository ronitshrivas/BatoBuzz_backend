namespace BatoBuzz.Feed.Services;

/// Saves reel videos + thumbnails to disk. Kept separate from the image
/// IFileStorage so video's larger size limit and different allowed types don't
/// loosen the validation that protects image uploads. Swapping to object
/// storage (e.g. DigitalOcean Spaces) later means implementing just this
/// interface — nothing else in the reels code changes.
public interface IVideoStorage
{
    Task<string> SaveVideoAsync(IFormFile file, string subfolder, string fileName, CancellationToken ct);
    Task<string> SaveThumbnailAsync(IFormFile file, string subfolder, string fileName, CancellationToken ct);
}