using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Enums;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Feed.Services;

/// Drains the reel queue one job at a time and transcodes off the request
/// thread. SingleReader + one-at-a-time processing is deliberate: on a 2-core
/// VM we never want two FFmpeg runs competing with each other and the API.
public sealed class ReelTranscodeWorker : BackgroundService
{
    private readonly IReelJobQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReelTranscodeWorker> _log;

    public ReelTranscodeWorker(IReelJobQueue queue, IServiceScopeFactory scopes,
        ILogger<ReelTranscodeWorker> log)
        => (_queue, _scopes, _log) = (queue, scopes, log);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            try { await ProcessAsync(job, stoppingToken); }
            catch (Exception ex)
            {
                _log.LogError(ex, "Reel transcode failed for post {PostId}", job.PostId);
                await MarkAsync(job.PostId, ReelStatus.Failed, null, null, stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(ReelJob job, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var transcoder = scope.ServiceProvider.GetRequiredService<IReelTranscoder>();

        var result = await transcoder.TranscodeAsync(job.SourceAbsPath, job.RelFolder, ct);
        await MarkAsync(job.PostId, ReelStatus.Ready, result.HlsUrl, result.ThumbnailUrl, ct);
        _log.LogInformation("Reel {PostId} ready: {Hls}", job.PostId, result.HlsUrl);
    }

    private async Task MarkAsync(Guid postId, ReelStatus status, string? hlsUrl, string? thumbUrl, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return;

        post.ReelStatus = status;
        if (hlsUrl is not null) post.ReelHlsUrl = hlsUrl;
        if (thumbUrl is not null) post.ReelThumbnailUrl = thumbUrl;
        await db.SaveChangesAsync(ct);
    }
}