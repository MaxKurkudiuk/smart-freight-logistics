using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController(IOrderService orderService, ILogger<OrdersController> logger) : ControllerBase
{
    private readonly IOrderService _orderService = orderService;
    private readonly ILogger<OrdersController> _logger = logger;

    private bool TryGetCaller(out Guid userId, out string role)
    {
        userId = Guid.Empty;
        role = string.Empty;

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(sub, out userId))
            return false;

        role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;
        return true;
    }

    [HttpPost]
    [Authorize(Policy = "ClientPolicy")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out _))
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _orderService.CreateAsync(userId, request, ct);
            _logger.LogInformation("Order created {OrderId} by {ClientId}", response.Id, userId);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Create order bad request {ClientId}", userId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var role))
            return Unauthorized(new { message = "Invalid token." });

        var order = await _orderService.GetByIdAsync(id, userId, role, ct);
        if (order is null) return NotFound(new { message = "Order not found." });
        return Ok(order);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var role))
            return Unauthorized(new { message = "Invalid token." });

        var orders = await _orderService.ListAsync(userId, role, ct);
        return Ok(orders);
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var actorId, out var role))
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _orderService.UpdateStatusAsync(id, actorId, role, request, ct);
            _logger.LogInformation("Order {OrderId} status -> {NewStatus} by {ActorId}", id, request.NewStatus, actorId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Order not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (DomainException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
