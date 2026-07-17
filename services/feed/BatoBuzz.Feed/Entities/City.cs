namespace BatoBuzz.Feed.Entities;

/// Mirrors the Firestore `cities` collection the feed filter dropdown reads.
/// Id stays a string so existing city ids carry over unchanged.
public class City
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string FranchiseId { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}
