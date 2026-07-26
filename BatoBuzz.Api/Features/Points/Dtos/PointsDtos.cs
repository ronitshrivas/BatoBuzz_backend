using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Points.Dtos;

public sealed record AchievementDto(string Tier, string Label, string? Season);

/// The user's points summary — matches the app's UserPointsModel.
public sealed record UserPointsDto(
    Guid UserId,
    int TotalPoints,
    DateTime UpdatedAt,
    IReadOnlyList<AchievementDto> Achievements);

/// One row in the points history list.
public sealed record PointHistoryDto(
    Guid Id,
    string Action,
    int Points,
    string TargetId,
    Guid? PostId,
    Guid? MerchantId,
    DateTime CreatedAt);

public sealed record PointHistoryPage(
    IReadOnlyList<PointHistoryDto> Items,
    string? NextCursor,
    bool HasMore);

/// One leaderboard row.
public sealed record LeaderboardEntryDto(
    Guid UserId,
    string DisplayName,
    string? PhotoUrl,
    int TotalPoints,
    int Rank);

/// The caller's position — powers the "you're #4, leader has 320" banner.
public sealed record MyStandingDto(int Rank, int LeaderPoints, int MyPoints);

/// Award/revoke request. `action` is one of like|comment|share|qr_scan.
/// `targetId` is the post id (or merchant id for qr_scan) — the thing the
/// action was performed on, used for the once-only latch.
public sealed record PointActionRequest(
    [Required] string Action,
    [Required] string TargetId,
    Guid? PostId,
    Guid? MerchantId);

/// Result of awarding: whether points were actually granted (false if the
/// latch already existed) plus the new total.
public sealed record PointActionResult(bool Awarded, int PointsDelta, int TotalPoints);