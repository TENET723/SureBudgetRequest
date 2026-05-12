namespace SureBudgetRequest.Application.Abstractions.Security;

/// <summary>
/// Hashes and verifies passwords. The hash format is opaque to callers —
/// the implementation embeds algorithm, iteration count, and salt so it can
/// evolve without re-hashing existing users.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
