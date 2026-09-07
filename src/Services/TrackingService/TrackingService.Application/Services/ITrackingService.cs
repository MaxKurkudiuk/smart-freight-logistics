using TrackingService.Application.DTOs;

namespace TrackingService.Application.Services;

public interface ITrackingService
{
    Task<TrackingResponse?> GetAsync(Guid orderId, CancellationToken ct = default);

    Task<TrackingResponse> UpdateAsync(Guid orderId, UpdateTrackingRequest request, CancellationToken ct = default);
}
