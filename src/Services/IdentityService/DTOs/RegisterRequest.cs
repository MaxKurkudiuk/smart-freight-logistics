using System.ComponentModel.DataAnnotations;

namespace IdentityService.DTOs;

public sealed record RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; init; } = string.Empty;
}
