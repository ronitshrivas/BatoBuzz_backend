namespace BatoBuzz.Notifications.Entities;

/// An FCM registration token for push delivery. One account can have several
/// (multiple devices); a token is globally unique and can move between accounts
/// (reinstall, account switch), so it's upserted by token value.
public class DeviceToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; }      // "android" | "ios" | "web"
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}