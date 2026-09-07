using BuildingBlocks.Caching;
using FluentAssertions;
using Moq;
using TrackingService.Application.DTOs;
using TrackingService.Application.Services;
using TrackingService.Domain.Entities;
using TrackingService.Infrastructure.Repositories;

namespace TrackingService.Tests.Unit;

public sealed class TrackingServiceTests
{
    private static Mock<ICacheService> MockCacheNull()
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.GetAsync<TrackingEntry>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry?)null);
        m.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<TrackingEntry>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        m.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return m;
    }

    [Fact]
    public async Task UpdateAsync_ShouldCreate_WhenNotExists()
    {
        var repo = new Mock<ITrackingRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry?)null);
        repo.Setup(r => r.SetAsync(It.IsAny<TrackingEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry e, CancellationToken _) => e);

        var svc = new TrackingAppService(repo.Object);
        var orderId = Guid.NewGuid();
        var result = await svc.UpdateAsync(orderId, new UpdateTrackingRequest { Latitude = 50.45, Longitude = 30.52, SpeedKmh = 60 });

        result.OrderId.Should().Be(orderId);
        result.Latitude.Should().Be(50.45);
        repo.Verify(r => r.SetAsync(It.IsAny<TrackingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenNotFound()
    {
        var repo = new Mock<ITrackingRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry?)null);
        var svc = new TrackingAppService(repo.Object);
        var result = await svc.GetAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdate_WhenExists()
    {
        var orderId = Guid.NewGuid();
        var existing = TrackingEntry.Create(orderId, 50, 30);
        var repo = new Mock<ITrackingRepository>();
        repo.Setup(r => r.GetAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repo.Setup(r => r.SetAsync(It.IsAny<TrackingEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry e, CancellationToken _) => e);
        var svc = new TrackingAppService(repo.Object);

        var result = await svc.UpdateAsync(orderId, new UpdateTrackingRequest { Latitude = 51, Longitude = 31, Notes = "moved" });
        result.Latitude.Should().Be(51);
        result.Notes.Should().Be("moved");
    }
}
