namespace IdentityService.DTOs;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAt
);
