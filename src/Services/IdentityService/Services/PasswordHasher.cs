using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace IdentityService.Services;

/// <summary>
/// Production-ready password hasher utilizing PBKDF2 HMAC-SHA256.
/// Hybrid format: "iterations:saltHex:hashHex" — stores iterations for seamless upgrades.
/// - No ASP.NET Core Identity dependency
/// - Uses BCL Rfc2898DeriveBytes.Pbkdf2 static (not obsolete ctors)
/// - Fixed-time comparison via CryptographicOperations.FixedTimeEquals
/// </summary>
public sealed class PasswordHasher(IOptions<PasswordHasherOptions> options) : IPasswordHasher
{
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    private const char SegmentDelimiter = ':';

    private readonly PasswordHasherOptions _options = options.Value;

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be null or empty.", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(_options.SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _options.Iterations, Algorithm, _options.KeySize);

        return string.Join(
            SegmentDelimiter,
            _options.Iterations.ToString(),
            Convert.ToHexString(salt),
            Convert.ToHexString(hash)
        );
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        if (!TryParse(hashedPassword, out int iterations, out byte[]? salt, out byte[]? expectedHash))
            return false;

        // Use stored iteration count, not current options — so old hashes remain verifiable after upgrade
        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt!, iterations, Algorithm, expectedHash!.Length);

        return CryptographicOperations.FixedTimeEquals(expectedHash, inputHash);
    }

    public bool NeedsRehash(string hashedPassword)
    {
        if (!TryParse(hashedPassword, out int iterations, out _, out _))
            return false; // Bad format — caller will treat as invalid, not rehash

        return iterations != _options.Iterations;
    }

    private static bool TryParse(string hashedPassword, out int iterations, out byte[]? salt, out byte[]? hash)
    {
        iterations = 0;
        salt = null;
        hash = null;

        string[] segments = hashedPassword.Split(SegmentDelimiter);
        if (segments.Length != 3)
            return false;

        if (!int.TryParse(segments[0], out iterations) || iterations <= 0)
            return false;

        try
        {
            salt = Convert.FromHexString(segments[1]);
            hash = Convert.FromHexString(segments[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || hash.Length == 0)
            return false;

        return true;
    }
}
