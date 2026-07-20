using BatoBuzz.ServiceProvider.Enums;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.ServiceProvider.Extensions;

public static class EnumMapping
{
    public static string ToWire(this ProviderStatus s) => s switch
    {
        ProviderStatus.Approved => "approved",
        ProviderStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string ToWire(this ExperienceRange e) => e switch
    {
        ExperienceRange.TwoToFive => "2-5",
        ExperienceRange.FiveToTen => "5-10",
        ExperienceRange.TenPlus => "10+",
        _ => "0-2",
    };

    public static ExperienceRange ToExperience(string? raw) => (raw ?? "").Trim() switch
    {
        "2-5" => ExperienceRange.TwoToFive,
        "5-10" => ExperienceRange.FiveToTen,
        "10+" => ExperienceRange.TenPlus,
        _ => ExperienceRange.ZeroToTwo,
    };

    public static ProviderStatus ToStatus(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "approved" => ProviderStatus.Approved,
        "rejected" => ProviderStatus.Rejected,
        "pending" => ProviderStatus.Pending,
        _ => throw new AppException("Unknown status. Expected pending, approved, or rejected."),
    };
}