using BuildingBlocks.EventBus.IntegrationEvents;

namespace IntegrationService.Clients;

/// <summary>
/// 4.7-4.8 RPA bridge — Rest API to mock-rpa:5004
/// </summary>
public interface IRpaClient
{
    Task<bool> SubmitCustomsAsync(OrderCreatedIntegrationEvent @event, CancellationToken ct = default);
}
