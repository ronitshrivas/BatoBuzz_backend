namespace BatoBuzz.ServiceProvider.Enums;

/// Matches the Flutter `ProviderStatus` — wire values "pending"|"approved"|"rejected".
public enum ProviderStatus { Pending = 0, Approved = 1, Rejected = 2 }

/// Matches the Flutter `ExperienceRange` — wire values "0-2"|"2-5"|"5-10"|"10+".
public enum ExperienceRange { ZeroToTwo = 0, TwoToFive = 1, FiveToTen = 2, TenPlus = 3 }