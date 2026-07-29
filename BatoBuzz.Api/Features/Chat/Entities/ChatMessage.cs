using BatoBuzz.Chat.Enums;

namespace BatoBuzz.Chat.Entities;

/// One message in a thread. Supports text plus media (image/video/file/audio)
/// with the same metadata the app tracks, optional reply-to preview, and soft
/// delete (so a deleted message leaves a "message deleted" placeholder rather
/// than a hole in the conversation).
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public ChatThread? Thread { get; set; }

    public Guid SenderId { get; set; }
    public MessageType Type { get; set; }
    public string Text { get; set; } = string.Empty;

    // Media (null for plain text).
    public string? MediaUrl { get; set; }
    public string? MimeType { get; set; }
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? DurationMs { get; set; }        // voice/video length

    // Reply-to preview (denormalized so showing a reply needs no extra lookup).
    public Guid? ReplyToMessageId { get; set; }
    public Guid? ReplyToSenderId { get; set; }
    public string? ReplyToText { get; set; }
    public string? ReplyToType { get; set; }
    public string? ReplyToMediaUrl { get; set; }
    public string? ReplyToFileName { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Client-supplied ordering hint (the app's clientAt epoch ms) — lets the
    // sender's optimistic message and the server copy reconcile.
    public long ClientAt { get; set; }
}