namespace OrderService.Domain.Entities;

/// <summary>
/// Owned value object — persisted via OwnsOne in OrderDbContext.
/// </summary>
public sealed class CargoDetails
{
    public string CargoType { get; init; } = string.Empty; // Enum name as string, max 50 — init keeps EF OwnsOne immutable; mutate via Order.UpdateCargo()
    public DateTime? Deadline { get; init; }
    public decimal WeightKg { get; init; }
    public decimal? VolumeM3 { get; init; }
    public string Origin { get; init; } = string.Empty;      // max 200
    public string Destination { get; init; } = string.Empty; // max 200
    public string Description { get; init; } = string.Empty; // max 500
    public decimal? DeclaredValue { get; init; }
}
