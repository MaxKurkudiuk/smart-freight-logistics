namespace OrderService.Domain.Events;

/// <summary>
/// Marker for domain events — in-memory until Stage 6 MediatR dispatches.
/// No MassTransit dependency (Domain stays pure).
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
