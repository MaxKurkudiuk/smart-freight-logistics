using BuildingBlocks.Caching;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Application.Features.Orders.Commands.UpdateOrderStatus;

/// <summary>
/// 6.2 write side — carries OrderId/ActorId/Role from controller.
/// Handler body moved verbatim from Services/OrderService.cs:97 UpdateStatusAsync.
/// </summary>
public sealed record UpdateOrderStatusCommand(
    Guid OrderId,
    Guid ActorId,
    string ActorRole,
    UpdateStatusRequest Request) : IRequest<OrderResponse>;

public sealed class UpdateOrderStatusCommandHandler(
    IOrderRepository repo,
    ICacheService cache) : IRequestHandler<UpdateOrderStatusCommand, OrderResponse>
{
    private readonly IOrderRepository _repo = repo;
    private readonly ICacheService _cache = cache;

    public async Task<OrderResponse> Handle(UpdateOrderStatusCommand command, CancellationToken ct)
    {
        var orderId = command.OrderId;
        var actorId = command.ActorId;
        var actorRole = command.ActorRole;
        var request = command.Request;

        var order = await _repo.GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException("Order not found.");

        // 4.9 RpaBot can only set Customs on any order (IntegrationService bridge), bypasses owner check
        if (IsRpaBot(actorRole))
        {
            if (request.NewStatus != OrderStatus.Customs)
                throw new UnauthorizedAccessException("RpaBot can only set Customs.");
        }
        else if (!IsManager(actorRole) && order.ClientId != actorId)
        {
            // Only owner or manager can mutate — prevents Client A from cancelling Client B order
            throw new UnauthorizedAccessException("Not owner.");
        }

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
        refreshed.Status = request.NewStatus;
        refreshed.UpdatedAt = now;
        var mapped = Map(refreshed);

        // 5.7 invalidate Cache-Aside entries
        await _cache.RemoveAsync(CacheKeys.Order(orderId), ct);
        await _cache.SetAsync(CacheKeys.Order(orderId), mapped, CacheKeys.OrderTtl, ct);
        await _cache.RemoveAsync(CacheKeys.OrderList(refreshed.ClientId), ct);
        await _cache.RemoveAsync(CacheKeys.OrderListAll(), ct);

        return mapped;
    }

    private static bool IsManager(string role)
        => role.Equals("LogisticsManager", StringComparison.OrdinalIgnoreCase);

    private static bool IsRpaBot(string role)
        => role.Equals("RpaBot", StringComparison.OrdinalIgnoreCase);

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
