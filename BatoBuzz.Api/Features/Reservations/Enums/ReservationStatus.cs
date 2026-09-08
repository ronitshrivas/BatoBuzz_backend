namespace BatoBuzz.Reservations.Enums;

/// Lifecycle of a grab / reserve hold. The string names are serialized as-is
/// (JsonStringEnumConverter is registered app-wide) so they match the exact
/// status strings the Flutter apps read from the old Firestore `reservations`
/// documents: ACTIVE, COMPLETED, CANCELLED_BY_USER, CANCELLED_BY_MERCHANT,
/// EXPIRED, NO_SHOW.
public enum ReservationStatus
{
    ACTIVE = 0,
    COMPLETED = 1,
    CANCELLED_BY_USER = 2,
    CANCELLED_BY_MERCHANT = 3,
    EXPIRED = 4,
    NO_SHOW = 5,
}
