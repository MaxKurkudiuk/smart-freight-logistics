namespace TrackingService.Domain.ValueObjects;

/// <summary>
/// Value object for geographic coordinates.
/// </summary>
public sealed record GeoCoordinate(double Latitude, double Longitude)
{
    public static void Validate(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        if (longitude < -180 || longitude > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
    }

    public static GeoCoordinate Create(double latitude, double longitude)
    {
        Validate(latitude, longitude);
        return new GeoCoordinate(latitude, longitude);
    }
}
