using BuildingBlocks.Caching;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Application.Features.Orders.Queries.ListOrders;

/// <summary>
/// 6.3 read side — Cache-Aside list with IsManager switch.
/// Logic moved verbatim from Services/OrderService.cs:82 ListAsync.
/// Repository List* is AsNoTracking without History (6.3 read-model).
/// </summary>
public sealed record ListOrdersQuery(Guid RequesterId, string RequesterRole) : IRequest<IReadOnlyList<OrderResponse>>;

public sealed class ListOrdersQueryHandler(
    IOrderRepository repo,
    ICacheService cache) : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderResponse>>
{
    private readonly IOrderRepository _repo = repo;
    private readonly ICacheService _cache = cache;

    public async Task<IReadOnlyList<OrderResponse>> Handle(ListOrdersQuery query, CancellationToken ct)
    {
        var requesterId = query.RequesterId;
        var requesterRole = query.RequesterRole;

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
