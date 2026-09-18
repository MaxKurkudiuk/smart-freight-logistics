using MediatR;
using TrackingService.Application.DTOs;
using TrackingService.Domain.Entities;
using TrackingService.Infrastructure.Repositories;

namespace TrackingService.Application.Features.Tracking.Queries.GetTracking;

/// <summary>
/// 6.5 read side — Cache-Aside Redis read.
/// Logic moved verbatim from Services/TrackingService.cs:15 GetAsync.
/// </summary>
public sealed record GetTrackingQuery(Guid OrderId) : IRequest<TrackingResponse?>;

public sealed class GetTrackingQueryHandler(ITrackingRepository repo) : IRequestHandler<GetTrackingQuery, TrackingResponse?>
{
    private readonly ITrackingRepository _repo = repo;

    public async Task<TrackingResponse?> Handle(GetTrackingQuery query, CancellationToken ct)
    {
        if (query.OrderId == Guid.Empty) return null;
        var entry = await _repo.GetAsync(query.OrderId, ct);
        return entry is null ? null : Map(entry);
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
