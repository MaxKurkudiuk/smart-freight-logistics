using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OrderService.Tests.Integration;

/// <summary>
/// Test JWT helper — mirrors IdentityService JwtTokenGenerator but standalone for integration tests.
/// Uses same HS256 validation params as OrderService.API AddJwtAuthentication.
/// </summary>
public static class JwtHelper
{
    public const string Secret = "test-secret-must-be-at-least-32-chars-1234567890AB";
    public const string Issuer = "SmartFreightLogistics.Identity";
    public const string Audience = "SmartFreightLogistics.Gateways";

    public static string GenerateToken(Guid userId, string email, string fullName, string role, int expiryMinutes = 60)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string ClientToken(Guid? userId = null) =>
        GenerateToken(userId ?? Guid.NewGuid(), "client@example.com", "Test Client", "Client");

    public static string ManagerToken(Guid? userId = null) =>
        GenerateToken(userId ?? Guid.NewGuid(), "manager@example.com", "Test Manager", "LogisticsManager");

    public static string RpaBotToken(Guid? userId = null) =>
        GenerateToken(userId ?? Guid.NewGuid(), "rpa@example.com", "RPA Bot", "RpaBot");
}
