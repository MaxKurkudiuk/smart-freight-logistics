using BuildingBlocks.Caching;
using TrackingService.Domain.Entities;
using TrackingService.Infrastructure.Repositories;

namespace TrackingService.Infrastructure.Redis;

/// <summary>
/// Redis-backed repository via <see cref="ICacheService"/> (Cache-Aside).
/// Key: tracking:{orderId} TTL: 5m (CacheKeys.TrackingTtl).
/// No Postgres on Stage 5 — pure hot-data cache.
/// </summary>
public sealed class RedisTrackingRepository(ICacheService cache) : ITrackingRepository
{
    public async Task<TrackingEntry?> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty) return null;
        return await cache.GetAsync<TrackingEntry>(CacheKeys.Tracking(orderId), ct);
    }

    public async Task<TrackingEntry> SetAsync(TrackingEntry entry, CancellationToken ct = default)
    {
        await cache.SetAsync(CacheKeys.Tracking(entry.OrderId), entry, CacheKeys.TrackingTtl, ct);
        return entry;
    }

    public async Task<bool> DeleteAsync(Guid orderId, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty) return false;
        var existing = await GetAsync(orderId, ct);
        if (existing is null) return false;
        await cache.RemoveAsync(CacheKeys.Tracking(orderId), ct);
        return true;
    }
}
