using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities;

/// <summary>
/// State machine for OrderStatus (3.2). Terminal states Delivered/Cancelled have no outgoing transitions.
/// </summary>
public static class OrderStatusTransitions
{
    private static readonly IReadOnlyDictionary<OrderStatus, HashSet<OrderStatus>> Allowed = new Dictionary<OrderStatus, HashSet<OrderStatus>>
    {
        [OrderStatus.Created] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.InTransit, OrderStatus.Cancelled],
        [OrderStatus.InTransit] = [OrderStatus.Customs, OrderStatus.Cancelled],
        [OrderStatus.Customs] = [OrderStatus.Delivered, OrderStatus.Cancelled],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    public static bool CanTransit(OrderStatus from, OrderStatus to)
        => Allowed.TryGetValue(from, out var set) && set.Contains(to);

    public static void Ensure(OrderStatus from, OrderStatus to)
    {
        if (from == to) return; // idempotent no-op handled by caller; or allow as no-op
        if (!CanTransit(from, to))
            throw new DomainException($"Transition from {from} to {to} is not allowed.");
    }
}
