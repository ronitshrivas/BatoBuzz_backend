namespace BatoBuzz.Merchant.Enums;

/// Mirrors the merchantsRegistration.status the app's login gate reads.
/// Kept in sync with Identity's MerchantStatus — a merchant is Pending until an
/// admin approves, and only Approved merchants pass the app's entry gate.
public enum MerchantStatus { Pending = 0, Approved = 1, Rejected = 2 }
