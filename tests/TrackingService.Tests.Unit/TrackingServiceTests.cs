using FluentAssertions;
using Moq;
using TrackingService.Application.DTOs;
using TrackingService.Application.Features.Tracking.Commands.UpdateTracking;
using TrackingService.Application.Features.Tracking.Queries.GetTracking;
using TrackingService.Domain.Entities;
using TrackingService.Infrastructure.Repositories;

namespace TrackingService.Tests.Unit;

/// <summary>
/// 6.8 — same scenarios as the Stage 5 service tests, now exercising the
/// MediatR handlers directly. Validator cases live in
/// <see cref="TrackingCommandValidatorTests"/>.
/// </summary>
public sealed class TrackingServiceTests
{
    [Fact]
    public async Task UpdateCommandHandler_ShouldCreate_WhenNotExists()
    {
        var repo = new Mock<ITrackingRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry?)null);
        repo.Setup(r => r.SetAsync(It.IsAny<TrackingEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry e, CancellationToken _) => e);

        var handler = new UpdateTrackingCommandHandler(repo.Object);
        var orderId = Guid.NewGuid();
        var result = await handler.Handle(
            new UpdateTrackingCommand(orderId, new UpdateTrackingRequest { Latitude = 50.45, Longitude = 30.52, SpeedKmh = 60 }),
            CancellationToken.None);

        result.OrderId.Should().Be(orderId);
        result.Latitude.Should().Be(50.45);
        repo.Verify(r => r.SetAsync(It.IsAny<TrackingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetQueryHandler_ShouldReturnNull_WhenNotFound()
    {
        var repo = new Mock<ITrackingRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry?)null);
        var handler = new GetTrackingQueryHandler(repo.Object);
        var result = await handler.Handle(new GetTrackingQuery(Guid.NewGuid()), CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetQueryHandler_ShouldReturnEntry_WhenFound()
    {
        var orderId = Guid.NewGuid();
        var entry = TrackingEntry.Create(orderId, 50.45, 30.52, 60, "Kyiv depot");
        var repo = new Mock<ITrackingRepository>();
        repo.Setup(r => r.GetAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        var handler = new GetTrackingQueryHandler(repo.Object);

        var result = await handler.Handle(new GetTrackingQuery(orderId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.Latitude.Should().Be(50.45);
        result.Notes.Should().Be("Kyiv depot");
    }

    [Fact]
    public async Task UpdateCommandHandler_ShouldUpdate_WhenExists()
    {
        var orderId = Guid.NewGuid();
        var existing = TrackingEntry.Create(orderId, 50, 30);
        var repo = new Mock<ITrackingRepository>();
        repo.Setup(r => r.GetAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repo.Setup(r => r.SetAsync(It.IsAny<TrackingEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackingEntry e, CancellationToken _) => e);
        var handler = new UpdateTrackingCommandHandler(repo.Object);

        var result = await handler.Handle(
            new UpdateTrackingCommand(orderId, new UpdateTrackingRequest { Latitude = 51, Longitude = 31, Notes = "moved" }),
            CancellationToken.None);
        result.Latitude.Should().Be(51);
        result.Notes.Should().Be("moved");
    }
}
