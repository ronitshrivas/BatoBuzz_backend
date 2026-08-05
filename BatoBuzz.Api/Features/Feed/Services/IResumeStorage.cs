namespace BatoBuzz.Feed.Services;

/// Stores an applicant's resume image/snapshot. Behind an interface so it can
/// move to object storage later without touching application logic.
public interface IResumeStorage
{
    Task<string> SaveAsync(IFormFile file, Guid postId, Guid userId, CancellationToken ct);
}