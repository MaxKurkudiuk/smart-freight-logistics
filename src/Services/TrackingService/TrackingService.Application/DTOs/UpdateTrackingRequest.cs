using System.ComponentModel.DataAnnotations;

namespace TrackingService.Application.DTOs;

public sealed record UpdateTrackingRequest
{
    [Required]
    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(0, double.MaxValue)]
    public double? SpeedKmh { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}
