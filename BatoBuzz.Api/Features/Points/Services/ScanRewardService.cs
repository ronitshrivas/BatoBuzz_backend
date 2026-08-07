using System.Security.Claims;
using BatoBuzz.Points.Data;
using BatoBuzz.Points.Dtos;
using BatoBuzz.Points.Entities;
using BatoBuzz.Points.Enums;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Points.Services;

/// Scan rewards: a merchant scans a customer's BatoBuzz QR and the customer
/// earns points. This is the qr_scan point action (50 pts) applied to the
/// *scanned* user rather than the caller, so it can't reuse the normal
/// award-the-caller path — it writes the scanned user's points directly, reusing
/// the same latch table so a given merchant can only reward a given customer
/// once (the anti-farming guarantee, keyed on the merchant as the target).
public sealed class ScanRewardService : IScanRewardService
{
    private const string QrMarker = "byBatoBuzz";

    private readonly PointsDbContext _db;
    private readonly IHttpContextAccessor _http;

    public ScanRewardService(PointsDbContext db, IHttpContextAccessor http)
        => (_db, _http) = (db, http);

    public async Task<ScanRewardResult> AwardForScanAsync(string rawValue, CancellationToken ct)
    {
        var merchantId = RequireMerchant();

        // Only a BatoBuzz QR carries a reward.
        if (string.IsNullOrWhiteSpace(rawValue) || !rawValue.Contains(QrMarker, StringComparison.Ordinal))
            return new ScanRewardResult("invalid_code", 0, null, 0);

        var userId = ExtractUserId(rawValue);
        if (userId is null)
            return new ScanRewardResult("invalid_code", 0, null, 0);

        var pts = PointValues.ForAction(PointAction.QrScan);

        // Latch target = the merchant, so each merchant can reward this customer
        // once, but different merchants each can (matches the per-merchant intent).
        var targetId = merchantId.ToString();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var already = await _db.Latches.AnyAsync(
            l => l.UserId == userId && l.Action == PointAction.QrScan && l.TargetId == targetId, ct);
        if (already)
        {
            var current = await CurrentTotalAsync(userId.Value, ct);
            await tx.CommitAsync(ct);
            return new ScanRewardResult("already", 0, userId, current);
        }

        _db.Latches.Add(new PointLatch
        {
            UserId = userId.Value,
            Action = PointAction.QrScan,
            TargetId = targetId,
        });

        var total = await ApplyDeltaAsync(userId.Value, pts, ct);

        _db.History.Add(new PointHistoryEntry
        {
            UserId = userId.Value,
            Action = PointAction.QrScan,
            Points = pts,
            TargetId = targetId,
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
            return new ScanRewardResult("already", 0, userId, await CurrentTotalAsync(userId.Value, ct));
        }

        return new ScanRewardResult("success", pts, userId, total);
    }

    // ── internals ────────────────────────────────────────────────────────────

    /// The QR payload is the user id on its own line, alongside the marker line.
    /// Take the first non-empty line that isn't the marker (matches the app).
    private static Guid? ExtractUserId(string raw)
    {
        foreach (var line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed == QrMarker) continue;
            if (Guid.TryParse(trimmed, out var id)) return id;
        }
        return null;
    }

    private Guid RequireMerchant()
    {
        var p = _http.HttpContext?.User;
        var isMerchant = string.Equals(
            p?.FindFirstValue(TokenClaims.AccountType), AppRoles.Merchant, StringComparison.OrdinalIgnoreCase);
        if (!isMerchant)
            throw AppException.Forbidden("Only merchants can scan customer QR codes.");

        return Guid.TryParse(p?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw AppException.Unauthorized("You must be signed in.");
    }

    private async Task<int> CurrentTotalAsync(Guid userId, CancellationToken ct)
        => await _db.UserPoints.AsNoTracking()
            .Where(p => p.UserId == userId).Select(p => (int?)p.TotalPoints)
            .FirstOrDefaultAsync(ct) ?? 0;

    private async Task<int> ApplyDeltaAsync(Guid userId, int delta, CancellationToken ct)
    {
        var row = await _db.UserPoints.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (row is null)
        {
            row = new UserPoints { UserId = userId, TotalPoints = 0 };
            _db.UserPoints.Add(row);
        }
        row.TotalPoints = Math.Max(0, row.TotalPoints + delta);
        row.UpdatedAt = DateTime.UtcNow;
        return row.TotalPoints;
    }
}