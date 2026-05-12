using System.Security.Cryptography;
using SureBudgetRequest.Application.Abstractions.Security;

namespace SureBudgetRequest.Infrastructure.Security;

/// <summary>
/// PBKDF2-SHA256 password hasher. No external dependencies.
///
/// Stored format (single string, fits in varchar(500)):
///   v1$&lt;iterations&gt;$&lt;base64(salt16)&gt;$&lt;base64(hash32)&gt;
///
/// Iterations and salt are embedded in the hash so we can raise the iteration
/// count or rotate parameters later without invalidating existing users.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // OWASP 2023 minimum for PBKDF2-HMAC-SHA256.
    // If this number ever changes, existing hashes remain valid (the value is
    // embedded in the stored string); only newly-set passwords use the new count.
    private const int Iterations = 210_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return string.Join('$',
            "v1",
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string storedHash, string password)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(password))
            return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "v1") return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
