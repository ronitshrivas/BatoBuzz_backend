using BatoBuzz.Points.Data;
using BatoBuzz.Points.Entities;
using BatoBuzz.Points.Enums;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Reservations.Services;

/// Adapts the Reservations points-port onto the Points database, writing the
/// customer's points the same way ScanRewardService does (reward a non-caller
/// user, latch for idempotency, record history). It lives in the Reservations
/// feature but depends only on the Points DbContext + entities, so the two
/// features stay decoupled through IReservationPoints.
///
/// The latch target is the reservation id, so each reservation can award or
/// penalise exactly once regardless of retries.
public sealed class PointsReservationAdapter : IReservationPoints
{
    private readonly PointsDbContext _db;

    public PointsReservationAdapter(PointsDbContext db) => _db = db;

    public async Task<int> AwardCompletionAsync(Guid userId, Guid merchantId, Guid reservationId, CancellationToken ct)
    {
        var pts = PointValues.ForAction(PointAction.QrScan);   // 50, the reservation reward
        var target = ReservationTarget(reservationId);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var already = await _db.Latches.AnyAsync(
            l => l.UserId == userId && l.Action == PointAction.QrScan && l.TargetId == target, ct);
        if (already)
        {
            await tx.CommitAsync(ct);
            return 0;
        }

        _db.Latches.Add(new PointLatch
        {
            UserId = userId,
            Action = PointAction.QrScan,
            TargetId = target,
        });

        await ApplyDeltaAsync(userId, pts, ct);

        _db.History.Add(new PointHistoryEntry
        {
            UserId = userId,
            Action = PointAction.QrScan,
            Points = pts,
            TargetId = target,
            MerchantId = merchantId,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return pts;
        }
        catch (DbUpdateException)
        {
            // Lost the race on the latch — someone already awarded it.
            await tx.RollbackAsync(ct);
            return 0;
        }
    }

    public async Task DeductNoShowAsync(Guid userId, Guid merchantId, Guid reservationId, int penalty, CancellationToken ct)
    {
        if (penalty <= 0) return;

        var target = NoShowTarget(reservationId);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Reuse the latch table so a repeated no-show for the same reservation
        // deducts only once. Action is left as QrScan for schema compatibility;
        // the distinct target ("noshow:{id}") keeps it separate from the award.
        var already = await _db.Latches.AnyAsync(
            l => l.UserId == userId && l.Action == PointAction.QrScan && l.TargetId == target, ct);
        if (already)
        {
            await tx.CommitAsync(ct);
            return;
        }

        _db.Latches.Add(new PointLatch
        {
            UserId = userId,
            Action = PointAction.QrScan,
            TargetId = target,
        });

        await ApplyDeltaAsync(userId, -penalty, ct);

        _db.History.Add(new PointHistoryEntry
        {
            UserId = userId,
            Action = PointAction.QrScan,
            Points = -penalty,
            TargetId = target,
            MerchantId = merchantId,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
        }
    }

    private static string ReservationTarget(Guid reservationId) => $"reservation:{reservationId}";
    private static string NoShowTarget(Guid reservationId) => $"noshow:{reservationId}";

    private async Task ApplyDeltaAsync(Guid userId, int delta, CancellationToken ct)
    {
        var row = await _db.UserPoints.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            row = new UserPoints { UserId = userId, TotalPoints = 0 };
            _db.UserPoints.Add(row);
        }
        row.TotalPoints = Math.Max(0, row.TotalPoints + delta);
        row.UpdatedAt = DateTime.UtcNow;
    }
}
