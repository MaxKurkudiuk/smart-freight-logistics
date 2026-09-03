using OrderService.Application.DTOs;

namespace OrderService.Application.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(Guid clientId, CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderResponse?> GetByIdAsync(Guid orderId, Guid requesterId, string requesterRole, CancellationToken ct = default);
    Task<IReadOnlyList<OrderResponse>> ListAsync(Guid requesterId, string requesterRole, CancellationToken ct = default);
    Task<OrderResponse> UpdateStatusAsync(Guid orderId, Guid actorId, string actorRole, UpdateStatusRequest request, CancellationToken ct = default);
}
