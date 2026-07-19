using BatoBuzz.Identity.Enums;

namespace BatoBuzz.Identity.Entities;

/// Auth record for an end-user (email/password or Google). Profile fields
/// (bio, location, etc.) live in the User service — Identity holds only what
/// is needed to authenticate.
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PhotoUrl { get; set; }

    // Null when the account was created purely via Google.
    public string? PasswordHash { get; set; }

    // Set when linked to a Google account; lets us match on re-login.
    public string? GoogleSubjectId { get; set; }

    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = new();
}