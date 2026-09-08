namespace BatoBuzz.Reservations.Services;

/// The Reservations feature's port into the points system. Completing a pickup
/// rewards the customer; a confirmed no-show deducts from them. The Reservations
/// service never touches the Points database directly — this port keeps that
/// write in one small adapter (PointsReservationAdapter) so the reservation
/// logic stays independent of the Points schema.
public interface IReservationPoints
{
    /// Award the reservation's completion reward (the qr_scan 50-pt value) to
    /// the customer, once per reservation. Idempotent on reservationId: a repeat
    /// awards nothing and reports awarded = 0.
    Task<int> AwardCompletionAsync(Guid userId, Guid merchantId, Guid reservationId, CancellationToken ct);

    /// Deduct the no-show penalty from the customer, once per reservation.
    /// Idempotent on reservationId. The total is floored at zero.
    Task DeductNoShowAsync(Guid userId, Guid merchantId, Guid reservationId, int penalty, CancellationToken ct);
}
