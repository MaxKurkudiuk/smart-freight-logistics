using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackingService.Application.DTOs;
using TrackingService.Application.Services;

namespace TrackingService.API.Controllers;

[ApiController]
[Route("api/tracking")]
[Authorize]
public sealed class TrackingController(ITrackingService trackingService, ILogger<TrackingController> logger) : ControllerBase
{
    private readonly ITrackingService _trackingService = trackingService;
    private readonly ILogger<TrackingController> _logger = logger;

    private bool TryGetCaller(out Guid userId, out string role)
    {
        userId = Guid.Empty;
        role = string.Empty;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out userId)) return false;
        role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;
        return true;
    }

    [HttpPut("{orderId:guid}")]
    [Authorize(Policy = "ClientPolicy")]
    [ProducesResponseType(typeof(TrackingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TrackingResponse>> Update(Guid orderId, [FromBody] UpdateTrackingRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out _))
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _trackingService.UpdateAsync(orderId, request, ct);
            _logger.LogInformation("Tracking updated {OrderId} by {UserId} lat:{Lat} lon:{Lon}", orderId, userId, request.Latitude, request.Longitude);
            return Ok(response);
        }
        catch (ArgumentException ex) when (ex is ArgumentOutOfRangeException)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(TrackingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrackingResponse>> GetById(Guid orderId, CancellationToken ct)
    {
        if (!TryGetCaller(out _, out _))
            return Unauthorized(new { message = "Invalid token." });

        var entry = await _trackingService.GetAsync(orderId, ct);
        if (entry is null) return NotFound(new { message = "Tracking not found." });
        return Ok(entry);
    }

    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok(new { status = "Healthy", service = "TrackingService" });
}
