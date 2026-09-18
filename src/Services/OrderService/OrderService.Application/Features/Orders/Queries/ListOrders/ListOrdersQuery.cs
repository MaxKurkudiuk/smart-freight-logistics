using BuildingBlocks.Caching;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;

namespace OrderService.Application.Features.Orders.Queries.ListOrders;

/// <summary>
/// 6.7 read side — Cache-Aside list with IsManager switch over IOrderReadRepository.
/// 6.3 logic (cache keys, TTL, ownership switch) unchanged; DB read is now a
/// column projection (OrderReadModel, no History join, AsNoTracking).
/// </summary>
public sealed record ListOrdersQuery(Guid RequesterId, string RequesterRole) : IRequest<IReadOnlyList<OrderResponse>>;

public sealed class ListOrdersQueryHandler(
    IOrderReadRepository readRepo,
    ICacheService cache) : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderResponse>>
{
    private readonly IOrderReadRepository _readRepo = readRepo;
    private readonly ICacheService _cache = cache;

    public async Task<IReadOnlyList<OrderResponse>> Handle(ListOrdersQuery query, CancellationToken ct)
    {
        var requesterId = query.RequesterId;
        var requesterRole = query.RequesterRole;

        var listKey = IsManager(requesterRole) ? CacheKeys.OrderListAll() : CacheKeys.OrderList(requesterId);
        var cached = await _cache.GetAsync<IReadOnlyList<OrderResponse>>(listKey, ct);
        if (cached is not null) return cached;

        var orders = await _readRepo.ListAsync(IsManager(requesterRole) ? null : requesterId, ct);
        await _cache.SetAsync(listKey, orders, CacheKeys.OrderListTtl, ct);
        return orders;
    }

    private static bool IsManager(string role)
        => role.Equals("LogisticsManager", StringComparison.OrdinalIgnoreCase);
}
