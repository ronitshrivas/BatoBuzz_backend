namespace BatoBuzz.Feed.Dtos.Feed;

/// Wire shape of a post. Field names match the keys `MerchantPostModel`
/// already reads in both Flutter apps (`postId`, `post`, `postType`, …), so the
/// models need no rewrite — only the source swaps from Firestore to HTTP.
///
/// The Firestore arrays (`likedBy`, `viewedBy`, `reportedBy`) are deliberately
/// not returned: they don't scale and they leak every actor's id to every
/// reader. `IsLiked` / `IsViewed` / `IsReported` carry the only bit the UI
/// actually needs — whether *the caller* acted — and the counts come from the
/// denormalized columns.
public sealed record PostDto(
    Guid PostId,
    Guid MerchantId,
    string MerchantName,
    string MerchantPhoto,
    string Category,
    string Post,
    string PostType,
    IReadOnlyList<string> ImageUrls,
    string? VideoUrl,
    string? ReelsUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,

    // ── Viewer-relative state
    bool IsLiked,
    bool IsViewed,
    bool IsReported,

    // ── Counters
    int LikeCount,
    int CommentCount,
    int ViewCount,

    // ── Pricing
    decimal? Price,
    decimal? PreviousPrice,
    decimal? DiscountedPrice,

    // ── Location / classification
    string BusinessAddress,
    string CityId,
    string CityName,
    string BusinessCategory,
    string? AdsCategory,

    // ── Reel
    string? ReelCaption,
    string? ReelVideoUrl,
    string? ReelThumbnailUrl,
    string? ReelHlsUrl,
    string ReelStatus,

    // ── Job
    string? JobTitle,
    string? CompanyName,
    string? JobLocation,
    decimal? SalaryFrom,
    decimal? SalaryTo,
    string? JobDescription,
    IReadOnlyList<string> JobSkills,
    string? JobPerks,
    string? EmploymentType,
    string? WorkMode,
    string? ExperienceLevel,
    bool IsUrgent,
    bool AllowPhoneCalls,
    bool AllowWhatsApp,
    string? ContactPhone,
    string? ContactEmail,
    IReadOnlyList<string> JobImageUrls,

    // ── Event
    string? EventTitle,
    string? EventDescription,
    string? EventLocation,
    DateTime? EventDate,
    string? EventCoverUrl,
    string? EventTicketType,
    decimal? EventPrice);