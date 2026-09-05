namespace OrderService.Domain.Events;

/// <summary>
/// Base for Order aggregate domain events.
/// </summary>
public abstract record OrderDomainEvent(Guid OrderId, DateTime OccurredAt) : IDomainEvent;
