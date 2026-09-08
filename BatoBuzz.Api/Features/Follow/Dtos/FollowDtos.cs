using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Follow.Dtos;

public sealed record SetFollowRequest([Required] Guid MerchantId, bool Follow);

/// Follow state + live follower count for one merchant, so the profile button
/// and the count render from a single call.
public sealed record FollowStateDto(Guid MerchantId, bool IsFollowing, int FollowerCount);

public sealed record FollowerCountDto(Guid MerchantId, int FollowerCount);

/// The set of merchant ids the caller follows (the app keeps this as a Set).
public sealed record FollowingIdsDto(IReadOnlyList<Guid> MerchantIds);
