using OrderService.Domain.Enums;

namespace OrderService.Domain.Events;

/// <summary>
/// Raised when Order.TransitionTo succeeds.
/// </summary>
public sealed record OrderStatusChangedDomainEvent(
    Guid OrderId,
    OrderStatus FromStatus,
    OrderStatus ToStatus,
    Guid ActorId,
    DateTime OccurredAt,
    string? Notes = null) : OrderDomainEvent(OrderId, OccurredAt);
