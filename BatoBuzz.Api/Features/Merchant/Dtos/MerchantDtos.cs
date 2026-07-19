using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Merchant.Dtos;

/// Full merchant profile as the app reads it back. Field names match the old
/// Firestore document keys so the app's merchant model needs no rewrite.
public sealed record MerchantProfileDto(
    Guid Id,
    Guid MerchantId,
    string Phone,
    string BusinessName,
    string BusinessEmail,
    IReadOnlyList<string> BusinessCategories,
    string BusinessCategory,
    string BusinessAddress,
    string BusinessPanNumber,
    string CityId,
    string CityName,
    string Ward,
    double? Latitude,
    double? Longitude,
    string? CitizenshipFrontUrl,
    string? CitizenshipBackUrl,
    string? PanCardUrl,
    string? OwnerPhotoUrl,
    string Status,
    string? RejectionReason,
    DateTime CreatedAt);

/// Registration payload. Sent as multipart/form-data because it carries the
/// four KYC files alongside the fields. The four IFormFile props are optional
/// so a merchant can finish KYC later, matching the app's nullable paths.
public sealed class CreateMerchantProfileRequest
{
    [Required] public string BusinessName { get; set; } = string.Empty;
    public string BusinessEmail { get; set; } = string.Empty;

    /// Repeated form field: businessCategories=Food&businessCategories=Retail
    public List<string> BusinessCategories { get; set; } = new();

    public string BusinessAddress { get; set; } = string.Empty;
    public string BusinessPanNumber { get; set; } = string.Empty;

    [Required] public string CityId { get; set; } = string.Empty;
    [Required] public string CityName { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public IFormFile? CitizenshipFront { get; set; }
    public IFormFile? CitizenshipBack { get; set; }
    public IFormFile? PanCard { get; set; }
    public IFormFile? OwnerPhoto { get; set; }
}

/// Profile edit — text fields only. Documents are replaced through the upload
/// endpoint so an edit can't silently wipe KYC by omitting a file.
public sealed record UpdateMerchantProfileRequest(
    string? BusinessName,
    string? BusinessEmail,
    List<string>? BusinessCategories,
    string? BusinessAddress,
    string? BusinessPanNumber,
    string? CityId,
    string? CityName,
    string? Ward,
    double? Latitude,
    double? Longitude);

/// Admin approve/reject.
public sealed record ReviewMerchantRequest(
    [Required] bool Approve,
    string? RejectionReason);

public sealed record MerchantStatusDto(Guid MerchantId, string Status, string? RejectionReason);
