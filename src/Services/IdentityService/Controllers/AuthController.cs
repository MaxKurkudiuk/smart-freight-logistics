using IdentityService.Data;
using IdentityService.DTOs;
using IdentityService.Entities;
using IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IdentityDbContext db, IPasswordHasher hasher, ILogger<AuthController> logger) : ControllerBase
{
    private readonly IdentityDbContext _db = db;
    private readonly IPasswordHasher _hasher = hasher;
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

        var response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        // 201 with Location to me endpoint (no token per spec)
        return CreatedAtAction(nameof(GetMePlaceholder), new { id = user.Id }, response);
    }

    // Placeholder for CreatedAtAction target until GET /me is implemented in step 2.4.
    // Keeps 201 Location header valid without requiring auth. Will be replaced by real GetMe.
    [HttpGet("users/{id:guid}", Name = "GetUserById")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GetMePlaceholder(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        return Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        });
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException != null && ex.InnerException.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
}
