namespace IntegrationService.Clients;

/// <summary>
/// 4.7-4.8 OrderService bridge — PUT /api/orders/{id}/status Customs via RpaBot JWT
/// </summary>
public interface IOrderStatusClient
{
    Task MarkCustomsAsync(Guid orderId, CancellationToken ct = default);
}
