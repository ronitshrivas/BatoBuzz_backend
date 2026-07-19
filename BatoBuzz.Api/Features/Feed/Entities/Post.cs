using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Entities;

/// A merchant post — the relational equivalent of a Firestore `MerchantPost`
/// document. Type-specific fields (job, event, reel) stay nullable on the same
/// row, mirroring the single-collection shape the Flutter apps already expect.
///
/// Counters (LikeCount, CommentCount, ViewCount) are denormalized so the feed
/// never has to aggregate across Likes/Comments/Views on read.
public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Author (denormalized from Identity so the feed needs no cross-service call)
    public Guid MerchantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantPhoto { get; set; } = string.Empty;

    // ── Core
    public string Category { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;   // maps to `post` on the wire
    public PostType PostType { get; set; } = PostType.Ads;
    public List<string> ImageUrls { get; set; } = new();
    public string? VideoUrl { get; set; }
    public string? ReelsUrl { get; set; }

    // ── Location / classification
    public string BusinessAddress { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string BusinessCategory { get; set; } = string.Empty;
    public string? AdsCategory { get; set; }

    // ── Pricing (ads)
    public decimal? Price { get; set; }
    public decimal? PreviousPrice { get; set; }
    public decimal? DiscountedPrice { get; set; }

    // ── Counters (denormalized)
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ViewCount { get; set; }

    // ── Reel fields
    public string? ReelCaption { get; set; }
    public string? ReelVideoUrl { get; set; }
    public string? ReelThumbnailUrl { get; set; }

    // ── Job fields
    public string? JobTitle { get; set; }
    public string? CompanyName { get; set; }
    public string? JobLocation { get; set; }
    public decimal? SalaryFrom { get; set; }
    public decimal? SalaryTo { get; set; }
    public string? JobDescription { get; set; }
    public List<string> JobSkills { get; set; } = new();
    public string? JobPerks { get; set; }
    public string? EmploymentType { get; set; }
    public string? WorkMode { get; set; }
    public string? ExperienceLevel { get; set; }
    public bool IsUrgent { get; set; }
    public bool AllowPhoneCalls { get; set; }
    public bool AllowWhatsApp { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public List<string> JobImageUrls { get; set; } = new();

    // ── Event fields
    public string? EventTitle { get; set; }
    public string? EventDescription { get; set; }
    public string? EventLocation { get; set; }
    public DateTime? EventDate { get; set; }
    public string? EventCoverUrl { get; set; }
    public string? EventTicketType { get; set; }
    public decimal? EventPrice { get; set; }

    // ── Lifecycle
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<PostLike> Likes { get; set; } = new();
    public List<PostComment> Comments { get; set; } = new();
    public List<PostView> Views { get; set; } = new();
    public List<PostReport> Reports { get; set; } = new();
}
