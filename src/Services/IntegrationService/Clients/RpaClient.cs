using BuildingBlocks.EventBus.IntegrationEvents;

namespace IntegrationService.Clients;

/// <summary>
/// 4.7 stub — real HttpClient + Polly in 4.8 (WaitAndRetry 3 + CircuitBreaker)
/// </summary>
public sealed class RpaClient : IRpaClient
{
    private readonly ILogger<RpaClient> _logger;

    public RpaClient(ILogger<RpaClient> logger) => _logger = logger;

    public Task<bool> SubmitCustomsAsync(OrderCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("RPA stub SubmitCustoms OrderId={OrderId} Cargo={CargoType} {Origin}->{Destination}",
            @event.OrderId, @event.CargoType, @event.Origin, @event.Destination);
        // 4.8 will POST to Rpa:BaseUrl api/customs/declarations with HttpClient + Polly
        return Task.FromResult(true);
    }
}
