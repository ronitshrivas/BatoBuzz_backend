using BatoBuzz.Merchant.Dtos;
using BatoBuzz.Merchant.Entities;

namespace BatoBuzz.Merchant.Extensions;

public static class MappingExtensions
{
    public static MerchantProfileDto ToDto(this MerchantProfile m) => new(
        Id: m.Id,
        MerchantId: m.MerchantId,
        Phone: m.Phone,
        BusinessName: m.BusinessName,
        BusinessEmail: m.BusinessEmail,
        BusinessCategories: m.BusinessCategories,
        BusinessCategory: m.BusinessCategory,
        BusinessAddress: m.BusinessAddress,
        BusinessPanNumber: m.BusinessPanNumber,
        CityId: m.CityId,
        CityName: m.CityName,
        Ward: m.Ward,
        Latitude: m.Latitude,
        Longitude: m.Longitude,
        CitizenshipFrontUrl: m.CitizenshipFrontUrl,
        CitizenshipBackUrl: m.CitizenshipBackUrl,
        PanCardUrl: m.PanCardUrl,
        OwnerPhotoUrl: m.OwnerPhotoUrl,
        Status: m.Status.ToString().ToLowerInvariant(),
        RejectionReason: m.RejectionReason,
        CreatedAt: m.CreatedAt);
}
