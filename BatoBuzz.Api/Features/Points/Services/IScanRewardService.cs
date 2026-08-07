using BatoBuzz.Points.Dtos;

namespace BatoBuzz.Points.Services;

public interface IScanRewardService
{
    Task<ScanRewardResult> AwardForScanAsync(string rawValue, CancellationToken ct);
}