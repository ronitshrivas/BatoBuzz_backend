namespace BatoBuzz.Identity.Services;

public interface IPasswordHasher
{
    string Hash(string plain);
    bool Verify(string plain, string hash);
}