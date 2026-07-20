using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.ServiceProvider.Dtos;

/// Provider as the app reads it. Field names match the Flutter
/// `ServiceProviderModel` keys so the model needs no rewrite.
public sealed record ProviderDto(
    Guid Id,
    Guid SubmittedById,
    string FullName,
    string Profession,
    string Phone,
    string WhatsApp,
    string ServiceArea,
    string Experience,
    IReadOnlyList<string> ServiceCategories,
    string About,
    bool AvailableNow,
    string PhotoUrl,
    string DocumentUrl,
    string Status,
    string ReviewNote,
    double RatingAverage,
    int RatingCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// Registration payload — multipart/form-data (carries photo + document).
public sealed class SubmitApplicationRequest
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] public string Profession { get; set; } = string.Empty;
    [Required] public string Phone { get; set; } = string.Empty;
    public string WhatsApp { get; set; } = string.Empty;
    public string ServiceArea { get; set; } = string.Empty;

    /// "0-2" | "2-5" | "5-10" | "10+"
    public string Experience { get; set; } = "0-2";

    /// Repeat the field per category: serviceCategories=Plumbing&serviceCategories=Wiring
    public List<string> ServiceCategories { get; set; } = new();

    public string About { get; set; } = string.Empty;
    public bool AvailableNow { get; set; }

    public IFormFile? Photo { get; set; }
    public IFormFile? Document { get; set; }
}

public sealed record ProviderStatusDto(Guid SubmittedById, string Status, string ReviewNote);

public sealed record ReviewProviderRequest(
    [Required] bool Approve,
    string? ReviewNote);

// ── Ratings / reviews ────────────────────────────────────────────────────
public sealed record ReviewDto(
    Guid Id,
    Guid ProviderId,
    Guid UserId,
    string Author,
    string AuthorPhotoUrl,
    double Rating,
    string Comment,
    bool CanEdit,
    DateTime CreatedAt);

public sealed record UpsertReviewRequest(
    [Range(1, 5)] double Rating,
    [MaxLength(2000)] string Comment);