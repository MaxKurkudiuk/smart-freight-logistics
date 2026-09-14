using BuildingBlocks.Caching;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Application.Features.Orders.Queries.GetOrderById;

/// <summary>
/// 6.3 read side — Cache-Aside before DB with ownership check.
/// Logic moved verbatim from Services/OrderService.cs:60 GetByIdAsync.
/// GetById keeps aggregate load (Include History) for UpdateStatus reload path.
/// </summary>
public sealed record GetOrderByIdQuery(Guid OrderId, Guid RequesterId, string RequesterRole) : IRequest<OrderResponse?>;

public sealed class GetOrderByIdQueryHandler(
    IOrderRepository repo,
    ICacheService cache) : IRequestHandler<GetOrderByIdQuery, OrderResponse?>
{
    private readonly IOrderRepository _repo = repo;
    private readonly ICacheService _cache = cache;

    public async Task<OrderResponse?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        var orderId = query.OrderId;
        var requesterId = query.RequesterId;
        var requesterRole = query.RequesterRole;

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
