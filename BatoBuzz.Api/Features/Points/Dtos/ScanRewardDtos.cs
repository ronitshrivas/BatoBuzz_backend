using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Points.Dtos;

/// A merchant scanning a customer's BatoBuzz QR. The raw scanned value is sent
/// as-is; the server validates the marker and extracts the user id, so the
/// client doesn't need to parse it.
public sealed record ScanRewardRequest([Required] string RawValue);

/// Outcome of a scan. `awarded` is false when this merchant already rewarded
/// this customer (the once-per-merchant latch), so the UI can say "already
/// scanned today" rather than double-counting.
public sealed record ScanRewardResult(
    string Status,           // "success" | "already" | "invalid_code" | "user_not_found"
    int PointsAwarded,
    Guid? UserId,
    int UserTotalPoints);