namespace BuildingBlocks.Caching;

/// <summary>
/// Centralized Redis key factory + TTL constants for Cache-Aside pattern.
/// </summary>
public static class CacheKeys
{
    public static readonly TimeSpan TrackingTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan OrderTtl = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan OrderListTtl = TimeSpan.FromMinutes(2);

    public static string Tracking(Guid orderId) => $"tracking:{orderId}";

    public static string Order(Guid id) => $"order:{id}";

    public static string OrderList(Guid clientId) => $"order:list:{clientId}";

    public static string OrderListAll() => "order:list:all";
}
