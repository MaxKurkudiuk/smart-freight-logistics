namespace IdentityService.Services;

/// <summary>
/// Abstraction for password hashing/verification.
/// Implementation uses PBKDF2 HMAC-SHA256 with configurable iterations.
/// Hash format (hybrid): "iterations:saltHex:hashHex" — self-contained, supports NeedsRehash.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password. Throws on null/empty.</summary>
    string HashPassword(string password);

    /// <summary>Verifies plaintext against stored hash. Returns false on null/bad format (no exception).</summary>
    bool VerifyPassword(string password, string hashedPassword);

    /// <summary>True when stored hash uses different iteration count than current options (needs rehash on login).</summary>
    bool NeedsRehash(string hashedPassword);
}
