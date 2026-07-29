
using BatoBuzz.Chat.Data;
using BatoBuzz.Chat.Dtos;
using BatoBuzz.Chat.Entities;
using BatoBuzz.Chat.Enums;
using BatoBuzz.Chat.Hubs;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Chat.Services;

public interface IChatService
{
    Task<ThreadDto> StartThreadAsync(Guid merchantId, CancellationToken ct);
    Task<IReadOnlyList<ThreadDto>> GetMyThreadsAsync(CancellationToken ct);
    Task<ThreadDto> GetThreadAsync(Guid threadId, CancellationToken ct);
    Task<MessagePage> GetMessagesAsync(Guid threadId, string? cursor, int pageSize, CancellationToken ct);
    Task<MessageDto> SendTextAsync(Guid threadId, SendTextRequest req, CancellationToken ct);
    Task<MessageDto> SendMediaAsync(Guid threadId, IFormFile file, string type, long clientAt, Guid? replyToId, CancellationToken ct);
    Task DeleteMessageAsync(Guid threadId, Guid messageId, CancellationToken ct);
    Task MarkReadAsync(Guid threadId, CancellationToken ct);
    Task SetTypingAsync(Guid threadId, bool isTyping, CancellationToken ct);
}

public sealed class ChatService : IChatService
{
    private readonly ChatDbContext _db;
    private readonly IChatActor _actor;
    private readonly IChatMediaStorage _media;
    private readonly IChatDirectory _directory;
    private readonly IHubContext<ChatHub> _hub;

    public ChatService(ChatDbContext db, IChatActor actor, IChatMediaStorage media,
        IChatDirectory directory, IHubContext<ChatHub> hub)
        => (_db, _actor, _media, _directory, _hub) = (db, actor, media, directory, hub);

    public async Task<ThreadDto> StartThreadAsync(Guid merchantId, CancellationToken ct)
    {
        if (_actor.IsMerchant)
            throw AppException.Forbidden("Merchants can't start conversations; customers reach out first.");

        var userId = _actor.Id;
        var existing = await _db.Threads
            .FirstOrDefaultAsync(t => t.UserId == userId && t.MerchantId == merchantId, ct);

        if (existing is null)
        {
            existing = new ChatThread { UserId = userId, MerchantId = merchantId };
            _db.Threads.Add(existing);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                existing = await _db.Threads
                    .FirstAsync(t => t.UserId == userId && t.MerchantId == merchantId, ct);
            }
        }
        return await ToThreadDtoAsync(existing, ct);
    }

    public async Task<IReadOnlyList<ThreadDto>> GetMyThreadsAsync(CancellationToken ct)
    {
        var me = _actor.Id;
        var isMerchant = _actor.IsMerchant;
        var threads = await _db.Threads.AsNoTracking()
            .Where(t => isMerchant ? t.MerchantId == me : t.UserId == me)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);

        var dtos = new List<ThreadDto>(threads.Count);
        foreach (var t in threads) dtos.Add(await ToThreadDtoAsync(t, ct));
        return dtos;
    }

    public async Task<ThreadDto> GetThreadAsync(Guid threadId, CancellationToken ct)
        => await ToThreadDtoAsync(await LoadParticipantThreadAsync(threadId, ct), ct);

    public async Task<MessagePage> GetMessagesAsync(Guid threadId, string? cursor, int pageSize, CancellationToken ct)
    {
        await LoadParticipantThreadAsync(threadId, ct);
        pageSize = pageSize is < 1 or > 100 ? 30 : pageSize;

        var q = _db.Messages.AsNoTracking().Where(m => m.ThreadId == threadId);
        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out var before))
            q = q.Where(m => m.CreatedAt < before);

        var rows = await q.OrderByDescending(m => m.CreatedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1].CreatedAt) : null;
        return new MessagePage(rows.Select(ToMessageDto).ToList(), next, hasMore);
    }

    public async Task<MessageDto> SendTextAsync(Guid threadId, SendTextRequest req, CancellationToken ct)
    {
        var thread = await LoadParticipantThreadAsync(threadId, ct);
        var text = (req.Text ?? "").Trim();
        if (text.Length == 0) throw new AppException("Message can't be empty.");

        var msg = new ChatMessage
        {
            ThreadId = threadId,
            SenderId = _actor.Id,
            Type = MessageType.Text,
            Text = text,
            ClientAt = req.ClientAt,
        };
        await ApplyReplyAsync(msg, req.ReplyToMessageId, threadId, ct);
        return await PersistAndBroadcastAsync(thread, msg, text, ct);
    }

    public async Task<MessageDto> SendMediaAsync(Guid threadId, IFormFile file, string type,
        long clientAt, Guid? replyToId, CancellationToken ct)
    {
        var thread = await LoadParticipantThreadAsync(threadId, ct);
        var msgType = MessageTypeMap.Parse(type);
        if (msgType == MessageType.Text)
            throw new AppException("Media messages need a type of image, video, file, or audio.");

        var stored = await _media.SaveAsync(file, threadId, msgType.ToWire(), ct);
        var msg = new ChatMessage
        {
            ThreadId = threadId,
            SenderId = _actor.Id,
            Type = msgType,
            Text = string.Empty,
            MediaUrl = stored.Url,
            MimeType = stored.MimeType,
            FileName = stored.FileName,
            FileSizeBytes = stored.SizeBytes,
            ClientAt = clientAt,
        };
        await ApplyReplyAsync(msg, replyToId, threadId, ct);

        var preview = msgType switch
        {
            MessageType.Image => "\ud83d\udcf7 Photo",
            MessageType.Video => "\ud83c\udfa5 Video",
            MessageType.Audio => "\ud83c\udf99\ufe0f Voice message",
            _ => "\ud83d\udcce " + stored.FileName,
        };
        return await PersistAndBroadcastAsync(thread, msg, preview, ct);
    }

    public async Task DeleteMessageAsync(Guid threadId, Guid messageId, CancellationToken ct)
    {
        await LoadParticipantThreadAsync(threadId, ct);
        var msg = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.ThreadId == threadId, ct)
            ?? throw AppException.NotFound("Message not found.");
        if (msg.SenderId != _actor.Id)
            throw AppException.Forbidden("You can only delete your own messages.");

        msg.IsDeleted = true; msg.Text = string.Empty; msg.MediaUrl = null;
        await _db.SaveChangesAsync(ct);
        await _hub.Clients.Group(ChatHub.GroupName(threadId)).SendAsync("MessageDeleted", threadId, messageId, ct);
    }

    public async Task MarkReadAsync(Guid threadId, CancellationToken ct)
    {
        var thread = await LoadParticipantThreadAsync(threadId, ct);
        if (_actor.IsMerchant) thread.UnreadForMerchant = 0; else thread.UnreadForUser = 0;
        await _db.SaveChangesAsync(ct);
        await _hub.Clients.Group(ChatHub.GroupName(threadId)).SendAsync("ThreadRead", threadId, _actor.Id, ct);
    }

    public async Task SetTypingAsync(Guid threadId, bool isTyping, CancellationToken ct)
    {
        await LoadParticipantThreadAsync(threadId, ct);
        await _hub.Clients.Group(ChatHub.GroupName(threadId)).SendAsync("Typing", threadId, _actor.Id, isTyping, ct);
    }

    private async Task<ChatThread> LoadParticipantThreadAsync(Guid threadId, CancellationToken ct)
    {
        var t = await _db.Threads.FirstOrDefaultAsync(x => x.Id == threadId, ct)
            ?? throw AppException.NotFound("Conversation not found.");
        var me = _actor.Id;
        var ok = _actor.IsMerchant ? t.MerchantId == me : t.UserId == me;
        if (!ok) throw AppException.Forbidden("This isn't your conversation.");
        return t;
    }

    private async Task ApplyReplyAsync(ChatMessage msg, Guid? replyToId, Guid threadId, CancellationToken ct)
    {
        if (replyToId is null) return;
        var r = await _db.Messages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == replyToId && m.ThreadId == threadId, ct);
        if (r is null) return;
        msg.ReplyToMessageId = r.Id;
        msg.ReplyToSenderId = r.SenderId;
        msg.ReplyToText = r.IsDeleted ? "" : r.Text;
        msg.ReplyToType = r.Type.ToWire();
        msg.ReplyToMediaUrl = r.IsDeleted ? null : r.MediaUrl;
        msg.ReplyToFileName = r.FileName;
    }

    private async Task<MessageDto> PersistAndBroadcastAsync(ChatThread thread, ChatMessage msg, string previewText, CancellationToken ct)
    {
        _db.Messages.Add(msg);
        thread.LastMessageText = previewText.Length > 500 ? previewText[..500] : previewText;
        thread.LastMessageSenderId = msg.SenderId;
        thread.LastMessageType = msg.Type.ToWire();
        thread.LastMessageAt = msg.CreatedAt;
        thread.UpdatedAt = msg.CreatedAt;
        if (_actor.IsMerchant) thread.UnreadForUser++; else thread.UnreadForMerchant++;
        await _db.SaveChangesAsync(ct);

        var dto = ToMessageDto(msg);
        await _hub.Clients.Group(ChatHub.GroupName(thread.Id)).SendAsync("ReceiveMessage", dto, ct);
        return dto;
    }

    private async Task<ThreadDto> ToThreadDtoAsync(ChatThread t, CancellationToken ct)
    {
        var iAmMerchant = _actor.IsMerchant;
        var otherId = iAmMerchant ? t.UserId : t.MerchantId;
        var other = await _directory.GetPartyAsync(otherId, isMerchant: !iAmMerchant, ct);
        var unreadForMe = iAmMerchant ? t.UnreadForMerchant : t.UnreadForUser;
        return new ThreadDto(
            t.Id, t.UserId, t.MerchantId, otherId, other.Name, other.PhotoUrl,
            t.LastMessageText, t.LastMessageSenderId, t.LastMessageType, t.LastMessageAt,
            unreadForMe, t.UpdatedAt);
    }

    private static MessageDto ToMessageDto(ChatMessage m) => new(
        m.Id, m.ThreadId, m.SenderId, m.Type.ToWire(),
        m.IsDeleted ? "" : m.Text,
        m.IsDeleted ? null : m.MediaUrl, m.MimeType, m.FileName, m.FileSizeBytes, m.DurationMs,
        m.IsDeleted,
        m.ReplyToMessageId is null ? null : new ReplyPreviewDto(
            m.ReplyToMessageId.Value, m.ReplyToSenderId ?? Guid.Empty,
            m.ReplyToText ?? "", m.ReplyToType ?? "text", m.ReplyToMediaUrl, m.ReplyToFileName),
        m.CreatedAt, m.ClientAt);

    private static string EncodeCursor(DateTime t)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(t.ToString("O")));

    private static bool TryDecodeCursor(string cursor, out DateTime before)
    {
        before = default;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out before);
        }
        catch { return false; }
    }
}