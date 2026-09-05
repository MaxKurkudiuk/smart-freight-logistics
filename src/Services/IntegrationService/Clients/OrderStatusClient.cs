namespace IntegrationService.Clients;

/// <summary>
/// 4.7 stub — real HttpClient + RpaBot JWT in 4.8 (PUT /api/orders/{id}/status Customs)
/// </summary>
public sealed class OrderStatusClient : IOrderStatusClient
{
    private readonly ILogger<OrderStatusClient> _logger;

    public OrderStatusClient(ILogger<OrderStatusClient> logger) => _logger = logger;

    public Task MarkCustomsAsync(Guid orderId, CancellationToken ct = default)
    {
        _logger.LogInformation("OrderStatus stub MarkCustoms OrderId={OrderId}", orderId);
        // 4.8 will PUT to OrderService:BaseUrl api/orders/{id}/status {newStatus=Customs} with RpaBot JWT + Polly
        return Task.CompletedTask;
    }
}
