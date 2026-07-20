using BatoBuzz.ServiceProvider.Dtos;
using BatoBuzz.ServiceProvider.Entities;

namespace BatoBuzz.ServiceProvider.Extensions;

public static class MappingExtensions
{
    public static ProviderDto ToDto(this ServiceProviderEntity p) => new(
        Id: p.Id,
        SubmittedById: p.SubmittedById,
        FullName: p.FullName,
        Profession: p.Profession,
        Phone: p.Phone,
        WhatsApp: p.WhatsApp,
        ServiceArea: p.ServiceArea,
        Experience: p.Experience.ToWire(),
        ServiceCategories: p.ServiceCategories,
        About: p.About,
        AvailableNow: p.AvailableNow,
        PhotoUrl: p.PhotoUrl,
        DocumentUrl: p.DocumentUrl,
        Status: p.Status.ToWire(),
        ReviewNote: p.ReviewNote,
        RatingAverage: Math.Round(p.RatingAverage, 2),
        RatingCount: p.RatingCount,
        CreatedAt: p.CreatedAt,
        UpdatedAt: p.UpdatedAt);

    public static ReviewDto ToDto(this ProviderReviewEntity r, Guid? viewerId) => new(
        Id: r.Id,
        ProviderId: r.ProviderId,
        UserId: r.UserId,
        Author: r.Author,
        AuthorPhotoUrl: r.AuthorPhotoUrl,
        Rating: r.Rating,
        Comment: r.Comment,
        CanEdit: viewerId is not null && viewerId == r.UserId,
        CreatedAt: r.CreatedAt);
}