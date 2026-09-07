using BuildingBlocks.Caching;
using FluentAssertions;
using TrackingService.Domain.Entities;
using TrackingService.Domain.ValueObjects;

namespace TrackingService.Tests.Unit;

public sealed class TrackingDomainTests
{
    [Fact]
    public void GeoCoordinate_Validate_ShouldThrow_WhenLatitudeOutOfRange()
    {
        var act = () => GeoCoordinate.Validate(100, 30);
        act.Should().Throw<ArgumentOutOfRangeException>();
        act = () => GeoCoordinate.Validate(-91, 30);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GeoCoordinate_Validate_ShouldThrow_WhenLongitudeOutOfRange()
    {
        var act = () => GeoCoordinate.Validate(50, 200);
        act.Should().Throw<ArgumentOutOfRangeException>();
        act = () => GeoCoordinate.Validate(50, -181);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TrackingEntry_Create_ShouldFail_WhenOrderIdEmpty()
    {
        var act = () => TrackingEntry.Create(Guid.Empty, 50.45, 30.52);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TrackingEntry_Create_ShouldFail_WhenLatOutOfRange()
    {
        var act = () => TrackingEntry.Create(Guid.NewGuid(), 91, 30);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TrackingEntry_Create_ShouldSetTimestamp_AndNotesTrimmed()
    {
        var entry = TrackingEntry.Create(Guid.NewGuid(), 50.45, 30.52, 60, "  Kyiv depot  ");
        entry.Latitude.Should().Be(50.45);
        entry.Longitude.Should().Be(30.52);
        entry.SpeedKmh.Should().Be(60);
        entry.Notes.Should().Be("Kyiv depot");
        entry.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TrackingEntry_Update_ShouldFail_WhenSpeedNegative()
    {
        var entry = TrackingEntry.Create(Guid.NewGuid(), 50, 30);
        var act = () => entry.Update(51, 31, -5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CacheKeys_ShouldHaveExpectedTtl_AndFormat()
    {
        CacheKeys.TrackingTtl.Should().Be(TimeSpan.FromMinutes(5));
        CacheKeys.OrderTtl.Should().Be(TimeSpan.FromMinutes(2));
        CacheKeys.OrderListTtl.Should().Be(TimeSpan.FromMinutes(2));
        var id = Guid.NewGuid();
        CacheKeys.Tracking(id).Should().Be($"tracking:{id}");
        CacheKeys.Order(id).Should().Be($"order:{id}");
    }
}
