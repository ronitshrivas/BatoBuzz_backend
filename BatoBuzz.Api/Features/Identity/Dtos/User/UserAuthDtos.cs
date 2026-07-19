using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Identity.Dtos.User;

public sealed record UserRegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    [Required] string DisplayName,
    string? Phone);

public sealed record UserLoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record GoogleLoginRequest(
    [Required] string IdToken);          // Google ID token from the Flutter app

public sealed record UserProfileDto(
    Guid Id, string DisplayName, string Email,
    string? Phone, string? PhotoUrl, bool EmailVerified, DateTime CreatedAt);