namespace BatoBuzz.Merchant.Entities;

/// A user's star rating (1-5) of a merchant. One per (merchant, user): the app
/// keys the doc `{merchantId}_{userId}` and re-rating overwrites, so a unique
/// index on (MerchantId, UserId) enforces one rating that gets updated.
public class MerchantRating
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MerchantId { get; set; }
    public Guid UserId { get; set; }

    public int Rating { get; set; }   // 1..5, clamped/rounded on write

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}