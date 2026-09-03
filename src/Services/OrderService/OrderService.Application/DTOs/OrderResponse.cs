using OrderService.Domain.Enums;

namespace OrderService.Application.DTOs;

public sealed record OrderResponse
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public OrderStatus Status { get; init; }
    public string StatusName => Status.ToString();
    public required CargoDetailsDto Cargo { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record StatusHistoryDto
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public OrderStatus FromStatus { get; init; }
    public OrderStatus ToStatus { get; init; }
    public DateTime ChangedAt { get; init; }
    public Guid ChangedBy { get; init; }
    public string? Notes { get; init; }
}
