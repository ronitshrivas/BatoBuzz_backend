using BatoBuzz.Merchant.Enums;

namespace BatoBuzz.Merchant.Entities;

/// Full merchant business profile + KYC — the relational equivalent of a
/// Firestore `merchantsRegistration` document.
///
/// Identity owns the login (phone + PIN hash + gate status); this owns
/// everything the multi-step registration screen collects. Linked by
/// MerchantId (Identity's MerchantAccount.Id, carried as the JWT subject).
public class MerchantProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// Identity's MerchantAccount.Id — the JWT subject. One profile per account.
    public Guid MerchantId { get; set; }

    public string Phone { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessEmail { get; set; } = string.Empty;

    /// The app sends a list; we keep the list (for filtering) and the joined
    /// string the old document also stored (for display).
    public List<string> BusinessCategories { get; set; } = new();
    public string BusinessCategory { get; set; } = string.Empty;

    public string BusinessAddress { get; set; } = string.Empty;
    public string BusinessPanNumber { get; set; } = string.Empty;

    public string CityId { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // ── KYC documents (served URLs; files live on disk) ────────────────────
    public string? CitizenshipFrontUrl { get; set; }
    public string? CitizenshipBackUrl { get; set; }
    public string? PanCardUrl { get; set; }
    public string? OwnerPhotoUrl { get; set; }

    // ── Approval lifecycle (mirrors Identity's gate) ───────────────────────
    public MerchantStatus Status { get; set; } = MerchantStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
