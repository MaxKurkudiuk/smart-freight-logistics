using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

public sealed record CreateOrderRequest
{
    [Required]
    [MaxLength(50)]
    public string CargoType { get; init; } = string.Empty;

    public DateTime? Deadline { get; init; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "WeightKg must be > 0")]
    public decimal WeightKg { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal? VolumeM3 { get; init; }

    [Required]
    [MaxLength(200)]
    public string Origin { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Destination { get; init; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? DeclaredValue { get; init; }
}
