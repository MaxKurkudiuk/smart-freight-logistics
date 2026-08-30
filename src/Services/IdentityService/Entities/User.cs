namespace IdentityService.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Store salted hashes only!
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // E.g., "Client", "LogisticsManager", "RpaBot"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
