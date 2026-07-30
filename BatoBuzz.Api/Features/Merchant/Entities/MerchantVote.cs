namespace BatoBuzz.Merchant.Entities;

/// A user's single award vote. The rule (from the app) is ONE vote per user
/// total — not one per merchant — and it can't be changed once cast. So the
/// primary key is the user id: a user can have at most one vote row, ever.
public class MerchantVote
{
    public Guid UserId { get; set; }       // primary key — one vote per user
    public Guid MerchantId { get; set; }   // who they voted for
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}