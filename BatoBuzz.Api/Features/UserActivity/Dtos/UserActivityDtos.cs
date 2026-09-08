namespace BatoBuzz.UserActivity.Dtos;

/// One row of the signed-in user's activity history — a comment they wrote or a
/// post they liked. Mirrors the app's UserActivityItem so the profile "activity"
/// screen renders unchanged. `Type` is "comment" or "like".
public sealed record UserActivityItemDto(
    string Type,
    Guid PostId,
    string PostTitle,
    string? Text,          // comment text; null for a like
    string? PostImageUrl,  // best available thumbnail; null if none
    DateTime Time);
