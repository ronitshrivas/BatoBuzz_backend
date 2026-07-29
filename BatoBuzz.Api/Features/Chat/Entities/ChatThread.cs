
namespace BatoBuzz.Chat.Entities;

/// A conversation between one user and one merchant. There is exactly one
/// thread per (user, merchant) pair — the app enforces this with a
/// deterministic id, and we mirror it with a unique index so both sides always
/// land on the same conversation.
public class ChatThread
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid MerchantId { get; set; }

    // Denormalized preview of the most recent message, for the thread list.
    public string? LastMessageText { get; set; }
    public Guid? LastMessageSenderId { get; set; }
    public string? LastMessageType { get; set; }
    public DateTime? LastMessageAt { get; set; }

    // Per-side unread counts (the app's unread.forUser / unread.forMerchant).
    public int UnreadForUser { get; set; }
    public int UnreadForMerchant { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ChatMessage> Messages { get; set; } = new();
}