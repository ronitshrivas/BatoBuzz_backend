using BatoBuzz.Chat.Dtos;
using BatoBuzz.Chat.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Chat.Controllers;

/// Chat over HTTP: threads, message history, sending, read state, typing.
/// Real-time delivery of what's sent here happens over the SignalR hub at
/// /hubs/chat — this controller persists and then the service fans out.
[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _chat;
    public ChatController(IChatService chat) => _chat = chat;

    /// User-only: find or create the conversation with a merchant.
    [HttpPost("threads")]
    public async Task<IActionResult> StartThread(StartThreadRequest req, CancellationToken ct)
        => Ok(ApiResponse<ThreadDto>.Ok(await _chat.StartThreadAsync(req.MerchantId, ct)));

    /// My conversations, most recently active first.
    [HttpGet("threads")]
    public async Task<IActionResult> MyThreads(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ThreadDto>>.Ok(await _chat.GetMyThreadsAsync(ct)));

    [HttpGet("threads/{threadId:guid}")]
    public async Task<IActionResult> GetThread(Guid threadId, CancellationToken ct)
        => Ok(ApiResponse<ThreadDto>.Ok(await _chat.GetThreadAsync(threadId, ct)));

    /// Message history, newest first, keyset-paginated.
    [HttpGet("threads/{threadId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid threadId, [FromQuery] string? cursor,
        [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => Ok(ApiResponse<MessagePage>.Ok(await _chat.GetMessagesAsync(threadId, cursor, pageSize, ct)));

    [HttpPost("threads/{threadId:guid}/messages")]
    public async Task<IActionResult> SendText(Guid threadId, SendTextRequest req, CancellationToken ct)
        => Ok(ApiResponse<MessageDto>.Ok(await _chat.SendTextAsync(threadId, req, ct)));

    /// Send an attachment (image/video/file/audio) as multipart form-data.
    [HttpPost("threads/{threadId:guid}/media")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> SendMedia(Guid threadId,
        [FromForm] IFormFile file, [FromForm] string type,
        [FromForm] long clientAt, [FromForm] Guid? replyToMessageId, CancellationToken ct)
        => Ok(ApiResponse<MessageDto>.Ok(
            await _chat.SendMediaAsync(threadId, file, type, clientAt, replyToMessageId, ct)));

    [HttpDelete("threads/{threadId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid threadId, Guid messageId, CancellationToken ct)
    {
        await _chat.DeleteMessageAsync(threadId, messageId, ct);
        return Ok(ApiResponse<object>.Ok(null, "Message deleted."));
    }

    [HttpPost("threads/{threadId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid threadId, CancellationToken ct)
    {
        await _chat.MarkReadAsync(threadId, ct);
        return Ok(ApiResponse<object>.Ok(null, "Marked read."));
    }

    [HttpPost("threads/{threadId:guid}/typing")]
    public async Task<IActionResult> Typing(Guid threadId, TypingRequest req, CancellationToken ct)
    {
        await _chat.SetTypingAsync(threadId, req.IsTyping, ct);
        return Ok(ApiResponse<object>.Ok(null));
    }
}