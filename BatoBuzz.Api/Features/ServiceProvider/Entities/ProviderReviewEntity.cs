namespace BatoBuzz.ServiceProvider.Entities;

public class ProviderReviewEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProviderId { get; set; }
    public ServiceProviderEntity? Provider { get; set; }

    public Guid UserId { get; set; }
    public string Author { get; set; } = "Customer";
    public string AuthorPhotoUrl { get; set; } = string.Empty;

    public double Rating { get; set; }
    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}