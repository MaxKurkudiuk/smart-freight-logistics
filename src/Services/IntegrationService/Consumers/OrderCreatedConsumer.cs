using BuildingBlocks.EventBus.IntegrationEvents;
using IntegrationService.Clients;
using MassTransit;

namespace IntegrationService.Consumers;

/// <summary>
/// 4.7 Consumes OrderCreatedIntegrationEvent → RPA → OrderService Customs
/// Retry via MassTransit UseMessageRetry(3,1s) in BuildingBlocks.EventBus
/// </summary>
public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly IRpaClient _rpaClient;
    private readonly IOrderStatusClient _orderStatusClient;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IRpaClient rpaClient,
        IOrderStatusClient orderStatusClient,
        ILogger<OrderCreatedConsumer> logger)
    {
        _rpaClient = rpaClient;
        _orderStatusClient = orderStatusClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var @event = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation("Consuming OrderCreatedIntegrationEvent OrderId={OrderId} ClientId={ClientId} Cargo={CargoType} {Origin}->{Destination}",
            @event.OrderId, @event.ClientId, @event.CargoType, @event.Origin, @event.Destination);

        try
        {
            var rpaResult = await _rpaClient.SubmitCustomsAsync(@event, ct);
            if (rpaResult)
            {
                await _orderStatusClient.MarkCustomsAsync(@event.OrderId, ct);
                _logger.LogInformation("Order {OrderId} marked Customs via OrderStatusClient", @event.OrderId);
            }
            else
            {
                _logger.LogWarning("RPA SubmitCustoms returned false for OrderId={OrderId} — not marking Customs", @event.OrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OrderCreatedIntegrationEvent OrderId={OrderId}", @event.OrderId);
            throw; // MassTransit Retry
        }
    }
}
