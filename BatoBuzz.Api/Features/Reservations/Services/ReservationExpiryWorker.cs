using BatoBuzz.Reservations.Data;
using BatoBuzz.Reservations.Entities;
using BatoBuzz.Reservations.Enums;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Reservations.Services;

/// Background sweep that expires ACTIVE holds past their 8-hour window and
/// returns their held stock. Runs every few minutes — matching the hosted-worker
/// pattern the Feed feature uses for reel transcoding. The merchant can still
/// mark a genuine no-show (with penalty) before or after expiry; this only
/// frees stock and moves the row out of ACTIVE so it stops blocking new grabs.
public sealed class ReservationExpiryWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReservationExpiryWorker> _log;

    public ReservationExpiryWorker(IServiceScopeFactory scopes, ILogger<ReservationExpiryWorker> log)
        => (_scopes, _log) = (scopes, log);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _log.LogError(ex, "Reservation expiry sweep failed.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();

        var now = DateTime.UtcNow;
        var expired = await db.Reservations
            .Where(r => r.Status == ReservationStatus.ACTIVE && r.ExpiresAt <= now)
            .Take(200)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var r in expired)
        {
            r.Status = ReservationStatus.EXPIRED;
            r.CancelledAt = now;

            var stock = await db.PostStocks.FirstOrDefaultAsync(s => s.PostId == r.PostId, ct);
            if (stock is null)
                db.PostStocks.Add(new PostStock { PostId = r.PostId, StockAvailable = r.Quantity });
            else
            {
                stock.StockAvailable += r.Quantity;
                stock.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
        _log.LogInformation("Expired {Count} reservation hold(s).", expired.Count);
    }
}
