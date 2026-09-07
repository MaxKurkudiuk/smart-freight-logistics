using TrackingService.Application.DTOs;
using TrackingService.Domain.Entities;
using TrackingService.Infrastructure.Repositories;

namespace TrackingService.Application.Services;

/// <summary>
/// Cache-Aside over Redis via <see cref="ITrackingRepository"/>.
/// No DB on Stage 5 — purely hot-data cache with 5m TTL.
/// </summary>
public sealed class TrackingAppService(ITrackingRepository repo) : ITrackingService
{
    private readonly ITrackingRepository _repo = repo;

    public async Task<TrackingResponse?> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty) return null;
        var entry = await _repo.GetAsync(orderId, ct);
        return entry is null ? null : Map(entry);
    }

    public async Task<TrackingResponse> UpdateAsync(Guid orderId, UpdateTrackingRequest request, CancellationToken ct = default)
    {
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
