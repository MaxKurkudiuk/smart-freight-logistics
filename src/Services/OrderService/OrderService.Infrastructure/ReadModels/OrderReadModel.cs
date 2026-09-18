using OrderService.Domain.Enums;

namespace OrderService.Infrastructure.ReadModels;

/// <summary>
/// 6.7 flat read-model — EF projects into this (AsNoTracking Select, no History join),
/// then maps to <c>OrderResponse</c>. Field set covers the full list DTO
/// (plan's core fields + Deadline/Volume/Description/Value/UpdatedAt).
/// </summary>
public sealed record OrderReadModel
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public OrderStatus Status { get; init; }
    public string CargoType { get; init; } = string.Empty;
    public decimal WeightKg { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime? Deadline { get; init; }
    public decimal? VolumeM3 { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal? DeclaredValue { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
