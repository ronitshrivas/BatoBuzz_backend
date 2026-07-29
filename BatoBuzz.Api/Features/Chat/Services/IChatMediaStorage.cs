namespace BatoBuzz.Chat.Services;

public readonly record struct StoredMedia(string Url, string MimeType, long SizeBytes, string FileName);

/// Saves chat attachments (images, files, voice). Behind an interface so it can
/// move to object storage later without touching chat logic. Path mirrors the
/// app's Firebase layout: chat/{threadId}/{kind}/{timestamp}_{name}.
public interface IChatMediaStorage
{
    Task<StoredMedia> SaveAsync(IFormFile file, Guid threadId, string kind, CancellationToken ct);
}