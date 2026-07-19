namespace BatoBuzz.Merchant.Services;

public interface IFileStorage
{
    /// Saves an uploaded file under a subfolder and returns the public URL
    /// (e.g. "/uploads/merchant_kyc/{id}/citizenship_front.jpg").
    Task<string> SaveAsync(IFormFile file, string subfolder, string fileName, CancellationToken ct);
}
