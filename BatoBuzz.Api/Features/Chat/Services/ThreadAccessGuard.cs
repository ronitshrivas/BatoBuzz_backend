using System.Security.Claims;
using BatoBuzz.Chat.Data;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Chat.Services;

public sealed class ThreadAccessGuard : IThreadAccessGuard
{
    private readonly ChatDbContext _db;
    public ThreadAccessGuard(ChatDbContext db) => _db = db;

    public async Task<bool> CanAccessAsync(Guid threadId, ClaimsPrincipal? user)
    {
        var raw = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var me)) return false;

        return await _db.Threads.AnyAsync(t =>
            t.Id == threadId && (t.UserId == me || t.MerchantId == me));
    }
}