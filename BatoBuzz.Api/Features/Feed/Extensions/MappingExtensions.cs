using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Entities;

namespace BatoBuzz.Feed.Extensions;

public static class MappingExtensions
{
    /// Viewer-relative flags are passed in rather than read off navigation
    /// properties: the feed query resolves them for the whole page in one pass,
    /// so mapping stays free of lazy loads and N+1s.
    public static PostDto ToDto(this Post p, bool isLiked, bool isViewed, bool isReported) =>
        new(
            PostId: p.Id,
            MerchantId: p.MerchantId,
            MerchantName: p.MerchantName,
            MerchantPhoto: p.MerchantPhoto,
            Category: p.Category,
            Post: p.Body,
            PostType: p.PostType.ToWire(),
            ImageUrls: p.ImageUrls,
            VideoUrl: p.VideoUrl,
            ReelsUrl: p.ReelsUrl,
            CreatedAt: p.CreatedAt,
            UpdatedAt: p.UpdatedAt,
            IsLiked: isLiked,
            IsViewed: isViewed,
            IsReported: isReported,
            LikeCount: p.LikeCount,
            CommentCount: p.CommentCount,
            ViewCount: p.ViewCount,
            Price: p.Price,
            PreviousPrice: p.PreviousPrice,
            DiscountedPrice: p.DiscountedPrice,
            BusinessAddress: p.BusinessAddress,
            CityId: p.CityId,
            CityName: p.CityName,
            BusinessCategory: p.BusinessCategory,
            AdsCategory: p.AdsCategory,
            ReelCaption: p.ReelCaption,
            ReelVideoUrl: p.ReelVideoUrl,
            ReelThumbnailUrl: p.ReelThumbnailUrl,
            JobTitle: p.JobTitle,
            CompanyName: p.CompanyName,
            JobLocation: p.JobLocation,
            SalaryFrom: p.SalaryFrom,
            SalaryTo: p.SalaryTo,
            JobDescription: p.JobDescription,
            JobSkills: p.JobSkills,
            JobPerks: p.JobPerks,
            EmploymentType: p.EmploymentType,
            WorkMode: p.WorkMode,
            ExperienceLevel: p.ExperienceLevel,
            IsUrgent: p.IsUrgent,
            AllowPhoneCalls: p.AllowPhoneCalls,
            AllowWhatsApp: p.AllowWhatsApp,
            ContactPhone: p.ContactPhone,
            ContactEmail: p.ContactEmail,
            JobImageUrls: p.JobImageUrls,
            EventTitle: p.EventTitle,
            EventDescription: p.EventDescription,
            EventLocation: p.EventLocation,
            EventDate: p.EventDate,
            EventCoverUrl: p.EventCoverUrl,
            EventTicketType: p.EventTicketType,
            EventPrice: p.EventPrice);

    public static CommentDto ToDto(this PostComment c, Guid? viewerId) =>
        new(
            CommentId: c.Id,
            PostId: c.PostId,
            ParentId: c.ParentId,
            AuthorId: c.AuthorId,
            AuthorType: c.AuthorType.ToWire(),
            AuthorName: c.AuthorName,
            AuthorPhoto: c.AuthorPhoto,
            Text: c.Text,
            ReplyCount: c.ReplyCount,
            CanEdit: viewerId is not null && viewerId == c.AuthorId,
            CreatedAt: c.CreatedAt,
            UpdatedAt: c.UpdatedAt);

    public static CityDto ToDto(this City c) =>
        new(c.Id, c.Name, c.District, c.Province, c.FranchiseId, c.Lat, c.Lng, c.ImageUrl);
}
