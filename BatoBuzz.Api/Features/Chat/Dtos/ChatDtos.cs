using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Chat.Dtos;

/// A thread as the app lists it. `otherParty*` is resolved to whichever side
/// the caller is NOT, so the UI can show "chat with X" without knowing roles.
public sealed record ThreadDto(
    Guid Id,
    Guid UserId,
    Guid MerchantId,
    Guid OtherPartyId,
    string OtherPartyName,
    string? OtherPartyPhoto,
    string? LastMessageText,
    Guid? LastMessageSenderId,
    string? LastMessageType,
    DateTime? LastMessageAt,
    int UnreadForMe,
    DateTime UpdatedAt);

public sealed record ReplyPreviewDto(
    Guid MessageId,
    Guid SenderId,
    string Text,
    string Type,
    string? MediaUrl,
    string? FileName);

/// A single message, matching the app's ThreadMessage.
public sealed record MessageDto(
    Guid Id,
    Guid ThreadId,
    Guid SenderId,
    string Type,
    string Text,
    string? MediaUrl,
    string? MimeType,
    string? FileName,
    long? FileSizeBytes,
    int? DurationMs,
    bool IsDeleted,
    ReplyPreviewDto? ReplyTo,
    DateTime CreatedAt,
    long ClientAt);

public sealed record MessagePage(
    IReadOnlyList<MessageDto> Items,
    string? NextCursor,
    bool HasMore);

/// Start (or fetch) the thread with a given merchant. The user side calls this
/// with a merchantId; the thread is found-or-created deterministically.
public sealed record StartThreadRequest([Required] Guid MerchantId);

/// Send a text message. Media is sent via the multipart upload endpoint instead.
public sealed record SendTextRequest(
    [Required, MaxLength(4000)] string Text,
    long ClientAt,
    Guid? ReplyToMessageId);

public sealed record TypingRequest(bool IsTyping);