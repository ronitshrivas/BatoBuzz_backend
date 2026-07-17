using System.ComponentModel.DataAnnotations;
using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Dtos.Feed;

/// Feed query. All filters optional; defaults match the apps' default feed
/// (latest first, every city, every post type).
public sealed record FeedQuery
{
    public string? CityId { get; init; }

    /// "ads" | "reels" | "job" | "event"
    public string? PostType { get; init; }

    /// Only applied when PostType == "ads".
    public string? AdsCategory { get; init; }

    /// "latest" | "oldest" | "mostViewed" | "forYou"
    public string SortBy { get; init; } = "latest";

    /// Free-text search across body, merchant name, job title and event title.
    public string? Search { get; init; }

    /// Restrict to one merchant — powers the merchant app's "my posts" tab.
    public Guid? MerchantId { get; init; }

    /// Opaque cursor from the previous page's NextCursor.
    public string? Cursor { get; init; }

    [Range(1, 50)]
    public int PageSize { get; init; } = 10;
}

/// Create/update payload. Type-specific blocks are validated in the service
/// against PostType, so a job post cannot be saved without a JobTitle.
public record CreatePostRequest
{
    [Required] public string Category { get; init; } = string.Empty;

    /// The post body (maps to `post` in the Flutter model).
    public string Post { get; init; } = string.Empty;

    /// "ads" | "reels" | "job" | "event"
    [Required] public string PostType { get; init; } = "ads";

    public List<string> ImageUrls { get; init; } = new();
    public string? VideoUrl { get; init; }
    public string? ReelsUrl { get; init; }

    public decimal? Price { get; init; }
    public decimal? PreviousPrice { get; init; }
    public decimal? DiscountedPrice { get; init; }

    public string BusinessAddress { get; init; } = string.Empty;
    public string CityId { get; init; } = string.Empty;
    public string CityName { get; init; } = string.Empty;
    public string BusinessCategory { get; init; } = string.Empty;
    public string? AdsCategory { get; init; }

    public string? ReelCaption { get; init; }
    public string? ReelVideoUrl { get; init; }
    public string? ReelThumbnailUrl { get; init; }

    public string? JobTitle { get; init; }
    public string? CompanyName { get; init; }
    public string? JobLocation { get; init; }
    public decimal? SalaryFrom { get; init; }
    public decimal? SalaryTo { get; init; }
    public string? JobDescription { get; init; }
    public List<string> JobSkills { get; init; } = new();
    public string? JobPerks { get; init; }
    public string? EmploymentType { get; init; }
    public string? WorkMode { get; init; }
    public string? ExperienceLevel { get; init; }
    public bool IsUrgent { get; init; }
    public bool AllowPhoneCalls { get; init; }
    public bool AllowWhatsApp { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public List<string> JobImageUrls { get; init; } = new();

    public string? EventTitle { get; init; }
    public string? EventDescription { get; init; }
    public string? EventLocation { get; init; }
    public DateTime? EventDate { get; init; }
    public string? EventCoverUrl { get; init; }
    public string? EventTicketType { get; init; }
    public decimal? EventPrice { get; init; }
}

/// Same fields as create; an update replaces the post's contents wholesale, so
/// the client should send the full object back rather than a delta.
public sealed record UpdatePostRequest : CreatePostRequest;

public sealed record ReportPostRequest([Required, MaxLength(500)] string Reason);

/// Result of toggling a like — lets the client reconcile optimistic UI.
public sealed record LikeResultDto(Guid PostId, bool IsLiked, int LikeCount);
