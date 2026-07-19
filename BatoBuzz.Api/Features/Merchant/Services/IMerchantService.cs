using BatoBuzz.Merchant.Dtos;

namespace BatoBuzz.Merchant.Services;

public interface IMerchantService
{
    Task<MerchantProfileDto> CreateAsync(CreateMerchantProfileRequest req, CancellationToken ct);
    Task<MerchantProfileDto> GetMineAsync(CancellationToken ct);
    Task<MerchantProfileDto> GetByIdAsync(Guid merchantId, CancellationToken ct);
    Task<MerchantStatusDto> GetMyStatusAsync(CancellationToken ct);
    Task<MerchantProfileDto> UpdateAsync(UpdateMerchantProfileRequest req, CancellationToken ct);

    // Admin
    Task<IReadOnlyList<MerchantProfileDto>> ListAsync(string? status, CancellationToken ct);
    Task<MerchantProfileDto> ReviewAsync(Guid merchantId, ReviewMerchantRequest req, CancellationToken ct);
}
