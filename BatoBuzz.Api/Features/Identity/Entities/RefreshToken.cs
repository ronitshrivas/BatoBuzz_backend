namespace BatoBuzz.Identity.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;   // opaque random string
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    // Exactly one of these is set, identifying the owner.
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }

    public Guid? MerchantAccountId { get; set; }
    public MerchantAccount? MerchantAccount { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}