using BatoBuzz.Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BatoBuzz.Chat.Hubs;

/// The real-time channel. Clients connect here (with their JWT as
/// ?access_token=), join the groups for their threads, and receive pushed
/// events:
///   ReceiveMessage(MessageDto)   — a new message in a joined thread
///   MessageDeleted(threadId, messageId)
///   ThreadRead(threadId, byActorId)
///   Typing(threadId, actorId, isTyping)
///
/// Sending happens over HTTP (the controller); the hub is purely for fan-out.
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IThreadAccessGuard _access;
    public ChatHub(IThreadAccessGuard access) => _access = access;

    public static string GroupName(Guid threadId) => $"thread:{threadId}";

    /// Join a thread's group to receive its events — but only if the caller is
    /// actually a participant. You can't subscribe to a stranger's chat.
    public async Task JoinThread(string threadId)
    {
        if (!Guid.TryParse(threadId, out var id)) return;
        if (!await _access.CanAccessAsync(id, Context.User)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(id));
    }

    public async Task LeaveThread(string threadId)
    {
        if (!Guid.TryParse(threadId, out var id)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(id));
    }
}