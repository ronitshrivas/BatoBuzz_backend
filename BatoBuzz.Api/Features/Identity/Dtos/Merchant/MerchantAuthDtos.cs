using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Identity.Dtos.Merchant;

public sealed record MerchantSignupRequest(
    [Required] string Phone,
    [Required, MinLength(4)] string Pin,
    [Required] string BusinessName,
    string? OwnerPhotoUrl);

public sealed record MerchantLoginRequest(
    [Required] string Phone,
    [Required] string Pin);

public sealed record MerchantProfileDto(
    Guid Id, string Phone, string BusinessName,
    string? OwnerPhotoUrl, string Status, DateTime CreatedAt);

public sealed record PhoneExistsResponse(bool Exists);