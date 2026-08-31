using IdentityService.Entities;

namespace IdentityService.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
    string GenerateToken(User user, out DateTime expiresAt);
}
