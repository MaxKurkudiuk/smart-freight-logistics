using OrderService.Application.DTOs;

namespace OrderService.Application.Interfaces;

/// <summary>
/// 6.7 read-model — DB-level projection for list queries (no History join, no tracking).
/// Cache-Aside stays in the query handler: cache is checked before this repository.
/// </summary>
public interface IOrderReadRepository
{
    /// <param name="clientId">Filter by owner; null lists all (LogisticsManager).</param>
    Task<IReadOnlyList<OrderResponse>> ListAsync(Guid? clientId, CancellationToken ct = default);
}
