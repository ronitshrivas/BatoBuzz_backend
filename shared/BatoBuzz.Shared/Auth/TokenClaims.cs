namespace BatoBuzz.Shared.Auth;

/// Custom claim type names shared across services.
public static class TokenClaims
{
    public const string AccountType = "account_type"; // "user" | "merchant"
    public const string MerchantStatus = "merchant_status"; // pending|approved|rejected
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