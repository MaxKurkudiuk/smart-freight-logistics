namespace OrderService.Application.DTOs;

public sealed record CargoDetailsDto
{
    public string CargoType { get; init; } = string.Empty;
    public DateTime? Deadline { get; init; }
    public decimal WeightKg { get; init; }
    public decimal? VolumeM3 { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal? DeclaredValue { get; init; }
}
