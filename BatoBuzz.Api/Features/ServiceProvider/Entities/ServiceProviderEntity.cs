using BatoBuzz.ServiceProvider.Enums;

namespace BatoBuzz.ServiceProvider.Entities;

public class ServiceProviderEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmittedById { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string WhatsApp { get; set; } = string.Empty;
    public string ServiceArea { get; set; } = string.Empty;
    public ExperienceRange Experience { get; set; } = ExperienceRange.ZeroToTwo;
    public List<string> ServiceCategories { get; set; } = new();
    public string About { get; set; } = string.Empty;
    public bool AvailableNow { get; set; }

    public string PhotoUrl { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;

    public ProviderStatus Status { get; set; } = ProviderStatus.Pending;
    public string ReviewNote { get; set; } = string.Empty;

    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<ProviderReviewEntity> Reviews { get; set; } = new();
}