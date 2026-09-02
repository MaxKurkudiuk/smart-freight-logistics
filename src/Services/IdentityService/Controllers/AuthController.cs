using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityService.Data;
using IdentityService.DTOs;
using IdentityService.Entities;
using IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IdentityDbContext db, IPasswordHasher hasher, IJwtTokenGenerator jwt, ILogger<AuthController> logger) : ControllerBase
{
    private readonly IdentityDbContext _db = db;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly IJwtTokenGenerator _jwt = jwt;
    private readonly ILogger<AuthController> _logger = logger;

    /// <summary>Register new user. Always creates Client role. Returns 201 without token.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        // DataAnnotations already validated by [ApiController]; extra trimming/normalization
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();

        // Check duplicate before hashing (saves CPU)
        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, ct);
        if (exists)
        {
            _logger.LogWarning("Register conflict: email already exists {Email}", email);
            return Conflict(new { message = "Email already exists." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = fullName,
            Role = "Client", // fixed per spec — no Role in request
            PasswordHash = _hasher.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogWarning(ex, "Register conflict on SaveChanges {Email}", email);
            return Conflict(new { message = "Email already exists." });
        }

        _logger.LogInformation("User registered {UserId} {Email} {Role}", user.Id, user.Email, user.Role);

        var response = new UserResponse(user.Id, user.Email, user.FullName, user.Role, user.CreatedAt);

        // 201 with Location to me endpoint (no token per spec)
        return CreatedAtAction(nameof(GetMePlaceholder), new { id = user.Id }, response);
    }

    /// <summary>Login with email/password. Returns JWT on success, 401 otherwise.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !_hasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for {Email}", email);
            return Unauthorized(new { message = "Invalid credentials." });
        }

        // Seamless rehash if iterations changed (e.g., 100k dev -> 600k prod)
        if (_hasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _hasher.HashPassword(request.Password);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Password rehashed for {UserId}", user.Id);
        }

        var token = _jwt.GenerateToken(user, out var expiresAt);

        _logger.LogInformation("User logged in {UserId} {Email}", user.Id, user.Email);

        var response = new AuthResponse(token, expiresAt, user.Id, user.Role);
        return Ok(response);
    }

    // Placeholder for CreatedAtAction Location header — anonymous, keeps 201 Location valid.
    // Real authenticated profile is GET /api/auth/me below.
    [HttpGet("users/{id:guid}", Name = "GetUserById")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GetMePlaceholder(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        var response = new UserResponse(user.Id, user.Email, user.FullName, user.Role, user.CreatedAt);
        return Ok(response);
    }

    /// <summary>Current authenticated user profile.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(new { message = "Invalid token." });

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound(new { message = "User not found." });

        var response = new UserResponse(user.Id, user.Email, user.FullName, user.Role, user.CreatedAt);
        return Ok(response);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException != null && ex.InnerException.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
}
