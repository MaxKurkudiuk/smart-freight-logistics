using MediatR;
using TrackingService.Application.DTOs;
using TrackingService.Domain.Entities;
using TrackingService.Infrastructure.Repositories;

namespace TrackingService.Application.Features.Tracking.Commands.UpdateTracking;

/// <summary>
/// 6.5 write side — carries OrderId from controller (ISender.Send).
/// Handler body moved verbatim from Services/TrackingService.cs:22 UpdateAsync.
/// </summary>
public sealed record UpdateTrackingCommand(Guid OrderId, UpdateTrackingRequest Request) : IRequest<TrackingResponse>;

public sealed class UpdateTrackingCommandHandler(ITrackingRepository repo) : IRequestHandler<UpdateTrackingCommand, TrackingResponse>
{
    private readonly ITrackingRepository _repo = repo;

    public async Task<TrackingResponse> Handle(UpdateTrackingCommand command, CancellationToken ct)
    {
        var orderId = command.OrderId;
        var request = command.Request;

        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));

        // DataAnnotations already validated by [ApiController]; domain validates again
        var existing = await _repo.GetAsync(orderId, ct);

        TrackingEntry entry;
        if (existing is null)
        {
            entry = TrackingEntry.Create(orderId, request.Latitude, request.Longitude, request.SpeedKmh, request.Notes);
        }
        else
        {
            existing.Update(request.Latitude, request.Longitude, request.SpeedKmh, request.Notes);
            entry = existing;
        }

        await _repo.SetAsync(entry, ct);

        // Optional future: publish TrackingUpdatedIntegrationEvent via IPublishEndpoint (dashboard)
        return Map(entry);
    }

    private static TrackingResponse Map(TrackingEntry e) => new()
    {
        OrderId = e.OrderId,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        SpeedKmh = e.SpeedKmh,
        Timestamp = e.Timestamp,
        Notes = e.Notes
    };
}
