namespace IdentityService.Services;

/// <summary>
/// Options for PBKDF2 password hasher. Bound from "PasswordHasher" section.
/// </summary>
public sealed class PasswordHasherOptions
{
    public const string SectionName = "PasswordHasher";

    /// <summary>PBKDF2 iteration count. Prod 600_000 (OWASP SHA256), Dev 100_000 for speed.</summary>
    public int Iterations { get; set; } = 600_000;

    /// <summary>Salt size in bytes (128-bit).</summary>
    public int SaltSize { get; set; } = 16;

    /// <summary>Subkey length in bytes (256-bit).</summary>
    public int KeySize { get; set; } = 32;
}
