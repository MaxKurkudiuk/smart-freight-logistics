using BuildingBlocks.Caching;
using BuildingBlocks.EventBus.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Application.Mappings;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.Events;

namespace OrderService.Application.Services;

public sealed class OrderService(IOrderRepository repo, IPublishEndpoint publishEndpoint, ICacheService cache) : IOrderService
{
    private readonly IOrderRepository _repo = repo;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ICacheService _cache = cache;

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

        // 4.5 publish Domain → Integration (flat DTO, no EF owned VO)
        var domainEvent = order.DomainEvents.OfType<OrderCreatedDomainEvent>().FirstOrDefault();
        if (domainEvent is not null)
        {
            var integration = domainEvent.ToIntegrationEvent();
            await _publishEndpoint.Publish(integration, ct);
            order.ClearDomainEvents();
        }

        var mapped = Map(order);
        // 5.7 Cache-Aside: populate single + invalidate lists
        await _cache.SetAsync(CacheKeys.Order(order.Id), mapped, CacheKeys.OrderTtl, ct);
        await _cache.RemoveAsync(CacheKeys.OrderList(clientId), ct);
        await _cache.RemoveAsync(CacheKeys.OrderListAll(), ct);

        return mapped;
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid orderId, Guid requesterId, string requesterRole, CancellationToken ct = default)
    {
        // 5.7 Cache-Aside: try cache first, but ownership check must still apply
        var cached = await _cache.GetAsync<OrderResponse>(CacheKeys.Order(orderId), ct);
        if (cached is not null)
        {
            if (!IsManager(requesterRole) && cached.ClientId != requesterId)
                return null;
            return cached;
        }

        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return null;

        if (!IsManager(requesterRole) && order.ClientId != requesterId)
            return null;

        var mapped = Map(order);
        await _cache.SetAsync(CacheKeys.Order(orderId), mapped, CacheKeys.OrderTtl, ct);
        return mapped;
    }

    public async Task<IReadOnlyList<OrderResponse>> ListAsync(Guid requesterId, string requesterRole, CancellationToken ct = default)
    {
        var listKey = IsManager(requesterRole) ? CacheKeys.OrderListAll() : CacheKeys.OrderList(requesterId);
        var cached = await _cache.GetAsync<IReadOnlyList<OrderResponse>>(listKey, ct);
        if (cached is not null) return cached;

        var orders = IsManager(requesterRole)
            ? await _repo.ListAllAsync(ct)
            : await _repo.ListByClientAsync(requesterId, ct);

        var mapped = (IReadOnlyList<OrderResponse>)[.. orders.Select(Map)];
        await _cache.SetAsync(listKey, mapped, CacheKeys.OrderListTtl, ct);
        return mapped;
    }

    public async Task<OrderResponse> UpdateStatusAsync(Guid orderId, Guid actorId, string actorRole, UpdateStatusRequest request, CancellationToken ct = default)
    {
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
