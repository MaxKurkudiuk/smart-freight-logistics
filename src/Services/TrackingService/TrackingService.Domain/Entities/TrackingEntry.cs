using TrackingService.Domain.ValueObjects;

namespace TrackingService.Domain.Entities;

/// <summary>
/// Real-time tracking entry for an order. Stored in Redis with TTL (CacheKeys.TrackingTtl = 5m).
/// OrderId is the primary key (tracking:{orderId} in Redis).
/// </summary>
public sealed class TrackingEntry
{
    public Guid OrderId { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public double? SpeedKmh { get; private set; }

    public DateTime Timestamp { get; private set; }

    public string? Notes { get; private set; }

    private TrackingEntry() { }

    private TrackingEntry(Guid orderId, double latitude, double longitude, double? speedKmh, DateTime timestamp, string? notes)
    {
        OrderId = orderId;
        Latitude = latitude;
        Longitude = longitude;
        SpeedKmh = speedKmh;
        Timestamp = timestamp;
        Notes = notes;
    }

    public static TrackingEntry Create(Guid orderId, double latitude, double longitude, double? speedKmh = null, string? notes = null)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        GeoCoordinate.Validate(latitude, longitude);
        if (speedKmh.HasValue && speedKmh.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(speedKmh), "Speed must be >= 0.");
        if (notes is not null && notes.Length > 500)
            throw new ArgumentException("Notes max 500 chars.", nameof(notes));

        return new TrackingEntry(orderId, latitude, longitude, speedKmh, DateTime.UtcNow, notes?.Trim());
    }

    public void Update(double latitude, double longitude, double? speedKmh = null, string? notes = null)
    {
        GeoCoordinate.Validate(latitude, longitude);
        if (speedKmh.HasValue && speedKmh.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(speedKmh), "Speed must be >= 0.");
        if (notes is not null && notes.Length > 500)
            throw new ArgumentException("Notes max 500 chars.", nameof(notes));

        Latitude = latitude;
        Longitude = longitude;
        SpeedKmh = speedKmh;
        Timestamp = DateTime.UtcNow;
        if (notes is not null) Notes = notes.Trim();
    }

    public GeoCoordinate ToCoordinate() => new(Latitude, Longitude);
}
