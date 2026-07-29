namespace BatoBuzz.Chat.Services;

public readonly record struct ChatParty(string Name, string? PhotoUrl);

/// Resolves the display name + photo of the other party in a thread. A user's
/// details come from Identity; a merchant's from the Merchant profile. This
/// crosses feature boundaries by design (Chat reads them), kept behind an
/// interface so those dependencies stay explicit.
public interface IChatDirectory
{
    /// isMerchant = true means "look this id up as a merchant" (the caller is a
    /// user, so the other party is the merchant).
    Task<ChatParty> GetPartyAsync(Guid partyId, bool isMerchant, CancellationToken ct);
}