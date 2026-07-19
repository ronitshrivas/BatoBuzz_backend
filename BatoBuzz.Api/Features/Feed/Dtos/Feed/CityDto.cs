namespace BatoBuzz.Feed.Dtos.Feed;

public sealed record CityDto(
    string Id,
    string Name,
    string District,
    string Province,
    string FranchiseId,
    double Lat,
    double Lng,
    string ImageUrl);
