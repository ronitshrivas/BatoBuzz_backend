using BatoBuzz.Feed.Dtos.Common;
using BatoBuzz.Feed.Dtos.Feed;

namespace BatoBuzz.Feed.Services;

public interface IPostService
{
    Task<PagedResult<PostDto>> GetFeedAsync(FeedQuery query, CancellationToken ct);
    Task<PostDto> GetByIdAsync(Guid postId, CancellationToken ct);
    Task<PostDto> CreateAsync(CreatePostRequest req, CancellationToken ct);
    Task<PostDto> UpdateAsync(Guid postId, UpdatePostRequest req, CancellationToken ct);
    Task DeleteAsync(Guid postId, CancellationToken ct);

    Task<LikeResultDto> ToggleLikeAsync(Guid postId, CancellationToken ct);
    Task RecordViewAsync(Guid postId, CancellationToken ct);
    Task ReportAsync(Guid postId, ReportPostRequest req, CancellationToken ct);
}
