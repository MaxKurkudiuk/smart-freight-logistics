namespace BuildingBlocks.EventBus.IntegrationEvents;

/// <summary>
/// Flat DTO for RabbitMQ — decouples transport from EF owned VO CargoDetails.
/// Stable cross-service contract (Stage 4.3).
/// </summary>
public sealed record OrderCreatedIntegrationEvent(
    Guid OrderId,
    Guid ClientId,
    string CargoType,
    decimal WeightKg,
    string Origin,
    string Destination,
    DateTime CreatedAt);
