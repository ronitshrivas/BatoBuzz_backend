using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.PostInterest.Dtos;

/// Express interest in an event post. The post/merchant snapshots come from the
/// client (which has the post loaded); the server enforces one-per-post,
/// not-your-own-post, and event-only.
public sealed record ExpressInterestRequest(
    [Required] Guid PostId,
    [Required] Guid MerchantId,
    string PostType = "event",
    string PostTitle = "",
    string PostLocation = "",
    string MerchantName = "",
    string MerchantPhoto = "",
    // The interested user's contact snapshot, so the merchant can reach them.
    string UserName = "",
    string UserPhone = "",
    string UserEmail = "",
    string UserPhoto = "");

public sealed record InterestStateDto(Guid PostId, bool HasInterest, int InterestCount);

public sealed record InterestCountDto(Guid PostId, int InterestCount);

/// One interested user as the merchant's list reads it (matches the app's
/// PostInterestModel).
public sealed record PostInterestDto(
    Guid InterestId,
    Guid PostId,
    Guid UserId,
    Guid MerchantId,
    string PostType,
    string UserName,
    string UserPhone,
    string UserEmail,
    string UserPhoto,
    string PostTitle,
    string PostLocation,
    string MerchantName,
    string MerchantPhoto,
    DateTime InterestedAt);
