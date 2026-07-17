using BatoBuzz.Feed.Dtos.Common;
using BatoBuzz.Feed.Dtos.Feed;

namespace BatoBuzz.Feed.Services;

public interface ICommentService
{
    Task<PagedResult<CommentDto>> GetAsync(Guid postId, CommentQuery query, CancellationToken ct);
    Task<CommentDto> AddAsync(Guid postId, CreateCommentRequest req, CancellationToken ct);
    Task<CommentDto> UpdateAsync(Guid postId, Guid commentId, UpdateCommentRequest req, CancellationToken ct);
    Task DeleteAsync(Guid postId, Guid commentId, CancellationToken ct);
}
