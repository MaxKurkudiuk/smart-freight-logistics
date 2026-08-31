using System.ComponentModel.DataAnnotations;

namespace IdentityService.DTOs;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}
