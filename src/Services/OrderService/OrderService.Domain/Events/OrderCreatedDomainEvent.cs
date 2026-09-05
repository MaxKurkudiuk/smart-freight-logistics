using OrderService.Domain.Entities;

namespace OrderService.Domain.Events;

/// <summary>
/// Raised when Order.Create succeeds. Cargo is snapshot (owned VO) for handlers.
/// </summary>
public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    Guid ClientId,
    CargoDetails Cargo,
    DateTime OccurredAt) : OrderDomainEvent(OrderId, OccurredAt);
