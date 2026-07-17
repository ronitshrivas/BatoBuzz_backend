namespace BatoBuzz.Shared.Auth;

/// Custom claim type names shared across services.
public static class TokenClaims
{
    public const string AccountType = "account_type"; // "user" | "merchant"
    public const string MerchantStatus = "merchant_status"; // pending|approved|rejected

    // Carried so downstream services (Feed) can denormalize the author onto
    // posts and comments without calling back into Identity on every write.
    public const string DisplayName = "display_name";
    public const string PhotoUrl = "photo_url";
}

public static class AppRoles
{
    public const string User = "user";
    public const string Merchant = "merchant";
    public const string Admin = "admin";
}

public static class AppPolicies
{
    public const string ApprovedMerchant = "ApprovedMerchant";
}