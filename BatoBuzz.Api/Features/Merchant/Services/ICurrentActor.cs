namespace BatoBuzz.Merchant.Services;

/// The caller, resolved from the JWT minted by Identity.
public interface ICurrentActor
{
    bool IsAuthenticated { get; }
    Guid Id { get; }             // throws 401 if anonymous
    Guid? IdOrNull { get; }
    bool IsMerchant { get; }
    bool IsAdmin { get; }
    string Phone { get; }
    string Name { get; }
}
