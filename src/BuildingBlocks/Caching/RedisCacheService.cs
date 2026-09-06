using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BuildingBlocks.Caching;

/// <summary>
/// Cache-Aside implementation over <see cref="IDistributedCache"/> (Redis via AddStackExchangeRedisCache, InMemory fallback).
/// Serializes values as JSON. On miss/null delegates to factory and populates cache.
/// </summary>
public sealed class RedisCacheService(IDistributedCache cache) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        if (bytes is null || bytes.Length == 0) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        var opts = new DistributedCacheEntryOptions();
        if (ttl.HasValue) opts.AbsoluteExpirationRelativeToNow = ttl.Value;
        await cache.SetAsync(key, bytes, opts, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
        => await cache.RemoveAsync(key, ct);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var created = await factory();
        if (created is not null)
            await SetAsync(key, created, ttl, ct);

        return created!;
    }
}
