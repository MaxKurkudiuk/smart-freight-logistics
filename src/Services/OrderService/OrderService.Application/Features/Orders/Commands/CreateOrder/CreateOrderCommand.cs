using BuildingBlocks.Caching;
using BuildingBlocks.EventBus.IntegrationEvents;
using BuildingBlocks.Observability;
using MassTransit;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Application.Mappings;
using OrderService.Domain.Entities;
using OrderService.Domain.Events;

namespace OrderService.Application.Features.Orders.Commands.CreateOrder;

/// <summary>
/// 6.2 write side — carries ClientId from controller (ISender.Send).
/// Handler body moved verbatim from Services/OrderService.cs:20 CreateAsync.
/// </summary>
public sealed record CreateOrderCommand(Guid ClientId, CreateOrderRequest Request) : IRequest<OrderResponse>;

public sealed class CreateOrderCommandHandler(
    IOrderRepository repo,
    IPublishEndpoint publishEndpoint,
    ICacheService cache) : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IOrderRepository _repo = repo;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ICacheService _cache = cache;

    public async Task<OrderResponse> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var clientId = command.ClientId;
        var request = command.Request;

        // Basic owned VO validation already in Domain; DataAnnotations validated by [ApiController]
        if (request.Origin.Trim().Equals(request.Destination.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Origin and Destination must differ.");

        var cargo = new CargoDetails
        {
            CargoType = request.CargoType,
            Deadline = request.Deadline,
            WeightKg = request.WeightKg,
            VolumeM3 = request.VolumeM3,
            Origin = request.Origin.Trim(),
            Destination = request.Destination.Trim(),
            Description = request.Description.Trim(),
            DeclaredValue = request.DeclaredValue
        };

        var order = Order.Create(clientId, cargo);
        await _repo.AddAsync(order, ct);
        await _repo.SaveChangesAsync(ct);

        // 7.3 telemetry — count persisted orders (no-op until an OTel meter listener is attached)
        TelemetryMeters.OrdersCreated.Add(1, new KeyValuePair<string, object?>("cargo.type", request.CargoType));

        // 4.5 publish Domain → Integration (flat DTO, no EF owned VO)
        var domainEvent = order.DomainEvents.OfType<OrderCreatedDomainEvent>().FirstOrDefault();
        if (domainEvent is not null)
        {
            var integration = domainEvent.ToIntegrationEvent();
            await _publishEndpoint.Publish(integration, ct);
            order.ClearDomainEvents();
        }

        var mapped = Map(order);
        // 5.7 Cache-Aside: populate single + invalidate lists
        await _cache.SetAsync(CacheKeys.Order(order.Id), mapped, CacheKeys.OrderTtl, ct);
        await _cache.RemoveAsync(CacheKeys.OrderList(clientId), ct);
        await _cache.RemoveAsync(CacheKeys.OrderListAll(), ct);

        return mapped;
    }

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
