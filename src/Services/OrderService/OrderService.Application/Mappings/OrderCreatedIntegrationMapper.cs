using BuildingBlocks.EventBus.IntegrationEvents;
using OrderService.Domain.Events;

namespace OrderService.Application.Mappings;

/// <summary>
/// 4.3 mapping Domain → Integration (flat DTO, no EF owned VO leakage).
/// </summary>
public static class OrderCreatedIntegrationMapper
{
    public static OrderCreatedIntegrationEvent ToIntegrationEvent(this OrderCreatedDomainEvent e) => new(
        OrderId: e.OrderId,
        ClientId: e.ClientId,
        CargoType: e.Cargo.CargoType,
        WeightKg: e.Cargo.WeightKg,
        Origin: e.Cargo.Origin,
        Destination: e.Cargo.Destination,
        CreatedAt: e.OccurredAt);
}
