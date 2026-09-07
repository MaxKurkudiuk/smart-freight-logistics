namespace TrackingService.Application.DTOs;

public sealed record TrackingResponse
{
    public Guid OrderId { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double? SpeedKmh { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Notes { get; init; }
}
