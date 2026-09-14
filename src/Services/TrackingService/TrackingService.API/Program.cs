using BuildingBlocks.Caching.Extensions;
using BuildingBlocks.Logging;
using TrackingService.API.Extensions;
using TrackingService.Application.Services;
using TrackingService.Infrastructure.Redis;
using TrackingService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// 5.2-5.4 Redis Cache-Aside + JWT
builder.AddCaching();
builder.AddTrackingAuth();

// Application + Infrastructure
builder.Services.AddScoped<ITrackingRepository, RedisTrackingRepository>();
builder.Services.AddScoped<ITrackingService, TrackingAppService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSharedLogging();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "TrackingService" }));

app.Run();
