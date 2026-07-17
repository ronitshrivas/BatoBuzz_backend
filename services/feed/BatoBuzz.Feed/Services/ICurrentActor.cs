using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Services;

/// The caller, resolved from the JWT. Both apps hit the same feed endpoints, so
/// every read and write needs to know *who* is asking and from which app.
public interface ICurrentActor
{
    /// True when the request carried a valid token.
    bool IsAuthenticated { get; }

    /// Throws 401 when anonymous; use on write paths.
    Guid Id { get; }

    /// Null when anonymous — lets read paths stay public while still
    /// personalizing IsLiked/IsViewed for signed-in callers.
    Guid? IdOrNull { get; }

    AuthorType Type { get; }

    string Name { get; }
    string Photo { get; }

    bool IsMerchant { get; }
}
