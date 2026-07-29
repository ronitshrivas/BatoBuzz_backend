using BatoBuzz.Identity.Data;
using BatoBuzz.Merchant.Data;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Chat.Services;

public sealed class ChatDirectory : IChatDirectory
{
    private readonly IdentityDbContext _identity;
    private readonly MerchantDbContext _merchant;

    public ChatDirectory(IdentityDbContext identity, MerchantDbContext merchant)
        => (_identity, _merchant) = (identity, merchant);

    public async Task<ChatParty> GetPartyAsync(Guid partyId, bool isMerchant, CancellationToken ct)
    {
        if (isMerchant)
        {
            var m = await _merchant.Merchants.AsNoTracking()
                .Where(x => x.MerchantId == partyId)
                .Select(x => new { x.BusinessName, x.OwnerPhotoUrl })
                .FirstOrDefaultAsync(ct);
            return new ChatParty(m?.BusinessName ?? "Merchant", m?.OwnerPhotoUrl);
        }

        var u = await _identity.Users.AsNoTracking()
            .Where(x => x.Id == partyId)
            .Select(x => new { x.DisplayName, x.PhotoUrl })
            .FirstOrDefaultAsync(ct);
        return new ChatParty(u?.DisplayName ?? "BatoBuzz User", u?.PhotoUrl);
    }
}