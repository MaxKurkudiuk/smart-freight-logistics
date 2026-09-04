using Microsoft.EntityFrameworkCore;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services;

public sealed class OrderService(IOrderRepository repo) : IOrderService
{
    private readonly IOrderRepository _repo = repo;

    public async Task<OrderResponse> CreateAsync(Guid clientId, CreateOrderRequest request, CancellationToken ct = default)
    {
        // Basic owned VO validation already in Domain; DataAnnotations validated by [ApiController]
        if (request.Origin.Trim().Equals(request.Destination.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Origin and Destination must differ.");

        var cargo = new CargoDetails
        {
            CargoType = request.CargoType,
            Deadline = request.Deadline,
            WeightKg = request.WeightKg,
            VolumeM3 = request.VolumeM3,
            Origin = request.Origin.Trim(),
            Destination = request.Destination.Trim(),
            Description = request.Description.Trim(),
            DeclaredValue = request.DeclaredValue
        };

        var order = Order.Create(clientId, cargo);
        await _repo.AddAsync(order, ct);
        await _repo.SaveChangesAsync(ct);
        return Map(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid orderId, Guid requesterId, string requesterRole, CancellationToken ct = default)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return null;

        // Ownership: Client sees only own, LogisticsManager sees all (B2B standard  docs/main plan 3.5)
        if (!IsManager(requesterRole) && order.ClientId != requesterId)
            return null; // 404 semantics — hide existence

        return Map(order);
    }

    public async Task<IReadOnlyList<OrderResponse>> ListAsync(Guid requesterId, string requesterRole, CancellationToken ct = default)
    {
        var orders = IsManager(requesterRole)
            ? await _repo.ListAllAsync(ct)
            : await _repo.ListByClientAsync(requesterId, ct);

        return [.. orders.Select(Map)];
    }

    public async Task<OrderResponse> UpdateStatusAsync(Guid orderId, Guid actorId, string actorRole, UpdateStatusRequest request, CancellationToken ct = default)
    {
        var order = await _repo.GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException("Order not found.");

        // Only owner or manager can mutate — prevents Client A from cancelling Client B order
        if (!IsManager(actorRole) && order.ClientId != actorId)
            throw new UnauthorizedAccessException("Not owner.");

        // Validate transition via domain (throws DomainException -> 409)
        OrderStatusTransitions.Ensure(order.Status, request.NewStatus);
        if (order.Status == request.NewStatus)
        {
            // Idempotent: already in target status — return current without DB write
            return Map(order);
        }

        var fromStatus = order.Status;
        var now = DateTime.UtcNow;

        var history = new StatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            FromStatus = fromStatus,
            ToStatus = request.NewStatus,
            ChangedAt = now,
            ChangedBy = actorId,
            Notes = request.Notes
        };

        // Use ExecuteUpdate to avoid DbUpdateConcurrencyException on tracked entity (xmin/field mapping edge)
        var updated = await _repo.TryUpdateStatusWithHistoryAsync(orderId, request.NewStatus, now, history, ct);
        if (!updated)
            throw new DomainException("Concurrent update conflict — please retry.");

        // Reload for response
        var refreshed = await _repo.GetByIdAsync(orderId, ct) ?? order;
        // Patch in-memory for mapping if reload missed updated values (should not)
        refreshed.Status = request.NewStatus;
        refreshed.UpdatedAt = now;
        return Map(refreshed);
    }

    private static bool IsManager(string role)
        => role.Equals("LogisticsManager", StringComparison.OrdinalIgnoreCase);

    private static OrderResponse Map(Order o) => new()
    {
        Id = o.Id,
        ClientId = o.ClientId,
        Status = o.Status,
        Cargo = new CargoDetailsDto
        {
            CargoType = o.Cargo.CargoType,
            Deadline = o.Cargo.Deadline,
            WeightKg = o.Cargo.WeightKg,
            VolumeM3 = o.Cargo.VolumeM3,
            Origin = o.Cargo.Origin,
            Destination = o.Cargo.Destination,
            Description = o.Cargo.Description,
            DeclaredValue = o.Cargo.DeclaredValue
        },
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt
    };
}
