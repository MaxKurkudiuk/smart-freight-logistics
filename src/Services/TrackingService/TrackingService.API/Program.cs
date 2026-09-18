using BuildingBlocks.Caching.Extensions;
using BuildingBlocks.CQRS.Extensions;
using BuildingBlocks.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrackingService.API.Extensions;
using TrackingService.Application.Features.Tracking.Commands.UpdateTracking;
using TrackingService.Infrastructure.Redis;
using TrackingService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// 5.2-5.4 Redis Cache-Aside + JWT
builder.AddCaching();
builder.AddTrackingAuth();

// Application + Infrastructure
builder.Services.AddScoped<ITrackingRepository, RedisTrackingRepository>();
builder.Services.AddCqrs(typeof(UpdateTrackingCommand).Assembly);

// 7.2 HealthChecks — redis readiness + self liveness (replaces ad-hoc /health MapGet)
builder.Services.AddHealthChecks()
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Connection string 'Redis' not found."),
        name: "redis",
        tags: new[] { "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSharedLogging();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 7.2 health endpoints — /health (all), /health/ready (deps), /health/live (self)
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready"), ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live"), ResponseWriter = WriteHealthResponse });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

static async Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
    });
}
