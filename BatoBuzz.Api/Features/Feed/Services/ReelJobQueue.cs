using System.Threading.Channels;

namespace BatoBuzz.Feed.Services;

/// A reel awaiting transcoding: the post id + the absolute path of the raw MP4.
public sealed record ReelJob(Guid PostId, string SourceAbsPath, string RelFolder);

/// In-process job queue. A bounded channel so a flood of uploads can't grow
/// memory without limit — enqueue waits (briefly) if the worker is behind.
/// This lives in the single container; jobs pending at shutdown are lost, which
/// is fine because the raw MP4 is already saved and the reel can be re-queued.
public interface IReelJobQueue
{
    ValueTask EnqueueAsync(ReelJob job, CancellationToken ct);
    IAsyncEnumerable<ReelJob> DequeueAllAsync(CancellationToken ct);
}

public sealed class ReelJobQueue : IReelJobQueue
{
    private readonly Channel<ReelJob> _channel =
        Channel.CreateBounded<ReelJob>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ValueTask EnqueueAsync(ReelJob job, CancellationToken ct)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<ReelJob> DequeueAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}