using System.Net.Http.Json;
using BuildingBlocks.EventBus.IntegrationEvents;

namespace IntegrationService.Clients;

/// <summary>
/// 4.8 HttpClient + Polly WaitAndRetry 3×2^retry + CircuitBreaker 5/30s → mock-rpa:5004/api/customs
/// </summary>
public sealed class RpaClient : IRpaClient
{
    private readonly HttpClient _http;
    private readonly ILogger<RpaClient> _logger;

    public RpaClient(HttpClient http, ILogger<RpaClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> SubmitCustomsAsync(OrderCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("RPA SubmitCustoms OrderId={OrderId} Cargo={CargoType} {Origin}->{Destination} Weight={Weight}",
            @event.OrderId, @event.CargoType, @event.Origin, @event.Destination, @event.WeightKg);

        try
        {
            var payload = new
            {
                orderId = @event.OrderId,
                clientId = @event.ClientId,
                cargoType = @event.CargoType,
                weightKg = @event.WeightKg,
                origin = @event.Origin,
                destination = @event.Destination,
                createdAt = @event.CreatedAt
            };

            var response = await _http.PostAsJsonAsync("api/customs/declarations", payload, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("RPA customs submitted for OrderId={OrderId}", @event.OrderId);
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("RPA customs failed for OrderId={OrderId} Status={Status} Body={Body}",
                @event.OrderId, response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPA SubmitCustoms exception OrderId={OrderId}", @event.OrderId);
            throw;
        }
    }
}
