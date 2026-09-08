using BatoBuzz.Reservations.Data;
using BatoBuzz.Reservations.Dtos;
using BatoBuzz.Reservations.Entities;
using BatoBuzz.Reservations.Enums;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Reservations.Services;

public interface IReservationService
{
    // User side
    Task<GrabResultDto> GrabAsync(GrabRequest req, CancellationToken ct);
    Task<GrabResultDto> CancelByUserAsync(Guid reservationId, CancellationToken ct);
    Task<GrabResultDto> UpdateQuantityAsync(Guid reservationId, int quantity, CancellationToken ct);
    Task<ReservationDto?> GetActiveHoldAsync(Guid postId, CancellationToken ct);
    Task<IReadOnlyList<ReservationDto>> GetMyReservationsAsync(CancellationToken ct);
    Task<int> GetMyActiveCountAsync(CancellationToken ct);
    Task<int?> GetStockAsync(Guid postId, CancellationToken ct);
    Task<BlockStatusDto> GetBlockStatusAsync(Guid merchantId, CancellationToken ct);

    // Merchant side
    Task<IReadOnlyList<ReservationDto>> GetMerchantReservationsAsync(CancellationToken ct);
    Task<ReservationActionResultDto> CompleteAsync(Guid reservationId, Guid? scannedUserId, CancellationToken ct);
    Task<GrabResultDto> CancelByMerchantAsync(Guid reservationId, CancellationToken ct);
    Task<ReservationActionResultDto> NoShowAsync(Guid reservationId, bool blacklist, string reason, CancellationToken ct);
}

public sealed class ReservationService : IReservationService
{
    // Server-authoritative constants — mirror the app's ReservationModel rules.
    private static readonly TimeSpan HoldDuration = TimeSpan.FromHours(8);
    private static readonly TimeSpan CancelWindow = TimeSpan.FromHours(1);
    private const int MaxActivePerUser = 3;
    private const int NoShowPenalty = 50;   // matches the 50-pt reward/penalty

    private readonly ReservationDbContext _db;
    private readonly ICurrentActor _actor;
    private readonly IReservationPoints _points;

    public ReservationService(ReservationDbContext db, ICurrentActor actor, IReservationPoints points)
        => (_db, _actor, _points) = (db, actor, points);

    // ── User: grab ─────────────────────────────────────────────────────────────

    public async Task<GrabResultDto> GrabAsync(GrabRequest req, CancellationToken ct)
    {
        var userId = _actor.Id;
        var qty = Math.Max(1, req.Quantity);

        if (req.MerchantId == default)
            return Fail("INVALID_REQUEST", "This item can't be reserved right now.");

        // Merchant block?
        var block = await _db.Blacklist.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MerchantId == req.MerchantId && x.UserId == userId && x.IsActive, ct);
        if (block is not null)
            return Fail("MERCHANT_RESTRICTED", "This merchant has restricted you from reserving.");

        // One active hold per (user, post).
        var existing = await _db.Reservations
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == req.PostId
                                      && r.Status == ReservationStatus.ACTIVE, ct);
        if (existing is not null)
            return Fail("ALREADY_RESERVED", "You already have an active hold on this item.");

        // At most 3 active holds overall.
        var activeCount = await _db.Reservations
            .CountAsync(r => r.UserId == userId && r.Status == ReservationStatus.ACTIVE, ct);
        if (activeCount >= MaxActivePerUser)
            return Fail("RESERVATION_LIMIT", $"You can hold up to {MaxActivePerUser} items at a time.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Stock ledger, created lazily from the client-supplied initial stock.
        var stock = await _db.PostStocks.FirstOrDefaultAsync(s => s.PostId == req.PostId, ct);
        if (stock is null)
        {
            var seed = req.InitialStock ?? 0;
            stock = new PostStock { PostId = req.PostId, StockAvailable = Math.Max(0, seed) };
            _db.PostStocks.Add(stock);
        }

        if (stock.StockAvailable < qty)
        {
            await tx.RollbackAsync(ct);
            return Fail("OUT_OF_STOCK", "This item is out of stock.");
        }

        stock.StockAvailable -= qty;   // hold the units
        stock.UpdatedAt = DateTime.UtcNow;

        var now = DateTime.UtcNow;
        var res = new Reservation
        {
            UserId = userId,
            MerchantId = req.MerchantId,
            PostId = req.PostId,
            ProductTitle = req.ProductTitle,
            ProductImage = req.ProductImage,
            ReservedPrice = req.ReservedPrice,
            Quantity = qty,
            Status = ReservationStatus.ACTIVE,
            CreatedAt = now,
            ExpiresAt = now.Add(HoldDuration),
            UserName = _actor.Name,
            UserPhoto = _actor.Photo,
            MerchantName = req.MerchantName,
            MerchantPhoto = req.MerchantPhoto,
        };
        _db.Reservations.Add(res);

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Fail("ALREADY_RESERVED", "You already have an active hold on this item.");
        }

        return new GrabResultDto(true, res.Id, res.ExpiresAt, null, "Reserved.");
    }

    // ── User: cancel (first-hour window) ─────────────────────────────────────

    public async Task<GrabResultDto> CancelByUserAsync(Guid reservationId, CancellationToken ct)
    {
        var userId = _actor.Id;
        var res = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, ct);
        if (res is null)
            return Fail("NOT_FOUND", "Reservation not found.");
        if (res.Status != ReservationStatus.ACTIVE)
            return Fail("NOT_ACTIVE", "This reservation is no longer active.");
        if (DateTime.UtcNow > res.CreatedAt.Add(CancelWindow))
            return Fail("CANCEL_WINDOW_CLOSED",
                "The cancellation window has closed — collect it or points may be deducted.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        res.Status = ReservationStatus.CANCELLED_BY_USER;
        res.CancelledAt = DateTime.UtcNow;
        await ReturnStockAsync(res.PostId, res.Quantity, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new GrabResultDto(true, res.Id, null, null, "Reservation cancelled.");
    }

    // ── User: update quantity (0 releases the hold) ──────────────────────────

    public async Task<GrabResultDto> UpdateQuantityAsync(Guid reservationId, int quantity, CancellationToken ct)
    {
        var userId = _actor.Id;
        var res = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, ct);
        if (res is null) return Fail("NOT_FOUND", "Reservation not found.");
        if (res.Status != ReservationStatus.ACTIVE)
            return Fail("NOT_ACTIVE", "This reservation is no longer active.");

        if (quantity <= 0)
            return await CancelByUserAsync(reservationId, ct);

        var delta = quantity - res.Quantity;   // >0 needs more stock, <0 returns some

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var stock = await _db.PostStocks.FirstOrDefaultAsync(s => s.PostId == res.PostId, ct);
        if (delta > 0)
        {
            if (stock is null || stock.StockAvailable < delta)
            {
                await tx.RollbackAsync(ct);
                return Fail("OUT_OF_STOCK", "Not enough stock to increase your hold.");
            }
            stock.StockAvailable -= delta;
            stock.UpdatedAt = DateTime.UtcNow;
        }
        else if (delta < 0 && stock is not null)
        {
            stock.StockAvailable += -delta;
            stock.UpdatedAt = DateTime.UtcNow;
        }

        res.Quantity = quantity;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new GrabResultDto(true, res.Id, res.ExpiresAt, null, "Updated.");
    }

    // ── User: reads ──────────────────────────────────────────────────────────

    public async Task<ReservationDto?> GetActiveHoldAsync(Guid postId, CancellationToken ct)
    {
        var userId = _actor.Id;
        var res = await _db.Reservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == postId
                                      && r.Status == ReservationStatus.ACTIVE, ct);
        return res is null ? null : Map(res);
    }

    public async Task<IReadOnlyList<ReservationDto>> GetMyReservationsAsync(CancellationToken ct)
    {
        var userId = _actor.Id;
        var rows = await _db.Reservations.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<int> GetMyActiveCountAsync(CancellationToken ct)
    {
        var userId = _actor.Id;
        return await _db.Reservations.AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.Status == ReservationStatus.ACTIVE, ct);
    }

    public async Task<int?> GetStockAsync(Guid postId, CancellationToken ct)
    {
        var v = await _db.PostStocks.AsNoTracking()
            .Where(s => s.PostId == postId).Select(s => (int?)s.StockAvailable)
            .FirstOrDefaultAsync(ct);
        return v;
    }

    public async Task<BlockStatusDto> GetBlockStatusAsync(Guid merchantId, CancellationToken ct)
    {
        var userId = _actor.Id;
        var block = await _db.Blacklist.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MerchantId == merchantId && x.UserId == userId && x.IsActive, ct);
        if (block is null) return new BlockStatusDto(false, null);
        var reason = string.IsNullOrWhiteSpace(block.Reason) ? "No reason given" : block.Reason.Trim();
        return new BlockStatusDto(true, reason);
    }

    // ── Merchant: list + actions ─────────────────────────────────────────────

    public async Task<IReadOnlyList<ReservationDto>> GetMerchantReservationsAsync(CancellationToken ct)
    {
        var merchantId = _actor.Id;
        var rows = await _db.Reservations.AsNoTracking()
            .Where(r => r.MerchantId == merchantId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(60)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<ReservationActionResultDto> CompleteAsync(Guid reservationId, Guid? scannedUserId, CancellationToken ct)
    {
        var merchantId = _actor.Id;
        var res = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.MerchantId == merchantId, ct);
        if (res is null)
            return new ReservationActionResultDto(false, false, 0, "Reservation not found.");

        // QR path: the scanned uid must match the reservation's customer.
        if (scannedUserId is not null && scannedUserId.Value != res.UserId)
            return new ReservationActionResultDto(false, false, 0, "This code doesn't match the reservation.");

        // Idempotent: already completed → report it, change nothing.
        if (res.Status == ReservationStatus.COMPLETED)
            return new ReservationActionResultDto(true, true, 0, "Already completed.");

        if (res.Status != ReservationStatus.ACTIVE)
            return new ReservationActionResultDto(false, false, 0, "This reservation can no longer be completed.");

        res.Status = ReservationStatus.COMPLETED;
        res.CompletedAt = DateTime.UtcNow;
        res.PointsAwarded = true;   // the held stock stays reduced (units handed over)
        await _db.SaveChangesAsync(ct);

        // Award in the Points DB via the port (idempotent on reservation id).
        var awarded = await _points.AwardCompletionAsync(res.UserId, merchantId, res.Id, ct);
        return new ReservationActionResultDto(true, false, awarded, "Pickup completed.");
    }

    public async Task<GrabResultDto> CancelByMerchantAsync(Guid reservationId, CancellationToken ct)
    {
        var merchantId = _actor.Id;
        var res = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.MerchantId == merchantId, ct);
        if (res is null) return Fail("NOT_FOUND", "Reservation not found.");
        if (res.Status != ReservationStatus.ACTIVE)
            return Fail("NOT_ACTIVE", "This reservation is no longer active.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        res.Status = ReservationStatus.CANCELLED_BY_MERCHANT;
        res.CancelledAt = DateTime.UtcNow;
        await ReturnStockAsync(res.PostId, res.Quantity, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new GrabResultDto(true, res.Id, null, null, "Reservation cancelled.");
    }

    public async Task<ReservationActionResultDto> NoShowAsync(Guid reservationId, bool blacklist, string reason, CancellationToken ct)
    {
        var merchantId = _actor.Id;
        var res = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.MerchantId == merchantId, ct);
        if (res is null)
            return new ReservationActionResultDto(false, false, 0, "Reservation not found.");

        // Only an active-but-expired hold can be marked no-show.
        if (res.Status is ReservationStatus.COMPLETED or ReservationStatus.NO_SHOW)
            return new ReservationActionResultDto(true, res.Status == ReservationStatus.NO_SHOW, 0, "Already resolved.");
        if (res.Status != ReservationStatus.ACTIVE)
            return new ReservationActionResultDto(false, false, 0, "This reservation can't be marked as a no-show.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        res.Status = ReservationStatus.NO_SHOW;
        res.CancelledAt = DateTime.UtcNow;
        await ReturnStockAsync(res.PostId, res.Quantity, ct);   // units go back to stock

        if (blacklist)
            await UpsertBlacklistAsync(merchantId, res.UserId, reason, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Deduct the penalty in the Points DB (idempotent on reservation id).
        await _points.DeductNoShowAsync(res.UserId, merchantId, res.Id, NoShowPenalty, ct);
        return new ReservationActionResultDto(true, false, 0, "Marked as no-show.");
    }

    // ── internals ──────────────────────────────────────────────────────────────

    private async Task ReturnStockAsync(Guid postId, int qty, CancellationToken ct)
    {
        var stock = await _db.PostStocks.FirstOrDefaultAsync(s => s.PostId == postId, ct);
        if (stock is null)
        {
            _db.PostStocks.Add(new PostStock { PostId = postId, StockAvailable = qty });
        }
        else
        {
            stock.StockAvailable += qty;
            stock.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task UpsertBlacklistAsync(Guid merchantId, Guid userId, string reason, CancellationToken ct)
    {
        var row = await _db.Blacklist
            .FirstOrDefaultAsync(x => x.MerchantId == merchantId && x.UserId == userId, ct);
        if (row is null)
        {
            _db.Blacklist.Add(new MerchantBlacklistEntry
            {
                MerchantId = merchantId,
                UserId = userId,
                IsActive = true,
                Reason = (reason ?? string.Empty).Trim(),
            });
        }
        else
        {
            row.IsActive = true;
            row.Reason = (reason ?? string.Empty).Trim();
        }
    }

    private static GrabResultDto Fail(string code, string message)
        => new(false, null, null, code, message);

    private static ReservationDto Map(Reservation r) => new(
        r.Id, r.UserId, r.MerchantId, r.PostId,
        r.ProductTitle, r.ProductImage, r.ReservedPrice, r.Quantity,
        r.Status.ToString(),
        r.CreatedAt, r.ExpiresAt, r.CompletedAt, r.CancelledAt,
        r.PointsAwarded,
        r.UserName, r.UserPhoto, r.MerchantName, r.MerchantPhoto);
}
