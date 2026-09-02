namespace IdentityService.DTOs;

public sealed record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string Role
);
