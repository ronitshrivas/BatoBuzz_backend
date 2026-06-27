using BatoBuzz.Identity.Enums;

namespace BatoBuzz.Identity.Entities;

/// Auth record for a merchant. The Flutter app logs in with phone + PIN
/// (previously faked as 977{phone}@merchant.batobuzz.com). Here phone is the
/// real identifier and the PIN is hashed. Full KYC/business profile lives in
/// the Merchant service; Identity keeps only the login + gate status.
public class MerchantAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;   // unique, normalized
    public string PinHash { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string? OwnerPhotoUrl { get; set; }

    // The login gate: merchant cannot enter the app until Approved.
    public MerchantStatus Status { get; set; } = MerchantStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = new();
}