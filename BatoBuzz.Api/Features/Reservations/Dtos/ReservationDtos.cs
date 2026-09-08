using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Reservations.Dtos;

// ── User side ────────────────────────────────────────────────────────────────

/// Grab one or more units of a post. Snapshots (title/image/price and the
/// post's current stock) come from the client, which already has the post
/// loaded; the server still enforces stock, the 3-hold cap and one-per-post.
public sealed record GrabRequest(
    [Required] Guid PostId,
    [Range(1, 99)] int Quantity = 1,
    string ProductTitle = "",
    string ProductImage = "",
    decimal ReservedPrice = 0,
    Guid MerchantId = default,
    string MerchantName = "",
    string MerchantPhoto = "",
    /// The post's current stock, used only to seed the ledger the first time
    /// this post is ever grabbed. Ignored once a ledger row exists.
    int? InitialStock = null);

/// The result of a grab, mirroring the app's GrabResult. On failure the caller
/// reads Code (OUT_OF_STOCK, ALREADY_RESERVED, RESERVATION_LIMIT,
/// MERCHANT_RESTRICTED, …) and Message.
public sealed record GrabResultDto(
    bool Success,
    Guid? ReservationId,
    DateTime? ExpiresAt,
    string? Code,
    string Message);

public sealed record UpdateQuantityRequest([Range(0, 99)] int Quantity);

/// Whether the caller is blocked by a merchant, and why. Null Reason means not
/// blocked.
public sealed record BlockStatusDto(bool Blocked, string? Reason);

// ── Merchant side ────────────────────────────────────────────────────────────

/// Complete a pickup. UserId is the uid decoded from the customer's QR (the
/// scan path); omit it for the in-person "mark arrived" path.
public sealed record CompleteReservationRequest(Guid? UserId = null);

public sealed record ReservationActionResultDto(
    bool Success,
    bool AlreadyCompleted,
    int PointsAwarded,
    string Message = "");

public sealed record NoShowRequest(bool Blacklist = false, string Reason = "");

// ── Shared read shape ────────────────────────────────────────────────────────

/// One reservation as both apps' `ReservationModel.fromFirestore` expects it.
/// `Status` is one of ACTIVE / COMPLETED / CANCELLED_BY_USER /
/// CANCELLED_BY_MERCHANT / EXPIRED / NO_SHOW.
public sealed record ReservationDto(
    Guid ReservationId,
    Guid UserId,
    Guid MerchantId,
    Guid PostId,
    string ProductTitle,
    string ProductImage,
    decimal ReservedPrice,
    int Quantity,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    bool PointsAwarded,
    string UserName,
    string UserPhoto,
    string MerchantName,
    string MerchantPhoto);
