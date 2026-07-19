using System.Security.Cryptography;

namespace BatoBuzz.Identity.Services;

/// PBKDF2 (SHA-256). Format: {iterations}.{saltB64}.{hashB64}
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string plain)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            plain, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string plain, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3) return false;
        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);
        var attempt = Rfc2898DeriveBytes.Pbkdf2(
            plain, salt, iterations, HashAlgorithmName.SHA256, key.Length);
        return CryptographicOperations.FixedTimeEquals(attempt, key);
    }
}