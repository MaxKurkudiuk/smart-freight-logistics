using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Caching.Extensions;

public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers Redis (StackExchange) as IDistributedCache when ConnectionStrings:Redis is present,
    /// otherwise falls back to InMemory. Registers ICacheService (Cache-Aside helper) as Scoped.
    /// Expects ConnectionStrings__Redis = "logistics-cache:6379,password=xxx" (from docker .env).
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisCs = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisCs) && !redisCs.Contains("YOUR_", StringComparison.Ordinal))
        {
            services.AddStackExchangeRedisCache(o => o.Configuration = redisCs);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddMemoryCache();
        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }

    public static WebApplicationBuilder AddCaching(this WebApplicationBuilder builder)
    {
        builder.Services.AddCaching(builder.Configuration);
        return builder;
    }

    /// <summary>
    /// For testing: force InMemory without touching configuration.
    /// </summary>
    public static IServiceCollection AddInMemoryCaching(this IServiceCollection services)
    {
        services.AddDistributedMemoryCache();
        services.AddMemoryCache();
        services.AddScoped<ICacheService, RedisCacheService>();
        return services;
    }
}
