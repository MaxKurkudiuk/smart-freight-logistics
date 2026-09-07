using TrackingService.Domain.Entities;

namespace TrackingService.Infrastructure.Repositories;

public interface ITrackingRepository
{
    Task<TrackingEntry?> GetAsync(Guid orderId, CancellationToken ct = default);

    Task<TrackingEntry> SetAsync(TrackingEntry entry, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid orderId, CancellationToken ct = default);
}
