using BuildingBlocks.Caching.Extensions;
using BuildingBlocks.CQRS.Extensions;
using BuildingBlocks.EventBus.Extensions;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability;
using BuildingBlocks.Observability.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderService.API.Extensions;
using OrderService.Application.Features.Orders.Commands.CreateOrder;
using OrderService.Application.Interfaces;
using OrderService.Infrastructure.ReadModels;
using OrderService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

builder.AddOrderDbContext();
builder.AddOrderAuth();
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddInMemoryCaching();
else
    builder.AddCaching();
if (builder.Environment.IsEnvironment("Testing"))
{
    // Fully isolated, no broker — uses standard MassTransit InMemory, no docker/.env file parsing
    builder.Services.AddMassTransit(x => x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx)));
}
else
{
    builder.AddEventBus();
}

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderReadRepository, OrderReadRepository>();
builder.Services.AddCqrs(typeof(CreateOrderCommand).Assembly);

// 7.3 OpenTelemetry — Npgsql + MassTransit sources, SmartFreight.Orders meter
builder.AddObservability("OrderService", sources =>
{
    sources.TraceSources.Add("Npgsql");
    sources.TraceSources.Add("MassTransit");
    sources.MeterNames.Add(TelemetryMeters.OrdersMeterName);
});

// 7.2 HealthChecks — postgres readiness + self liveness
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.GetOrderDbConnectionString(), name: "postgres", tags: new[] { "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

builder.Services.AddControllers();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Seed dev orders only in Development (idempotent, 3 orders for dev.client@example.com 3333...)
if (app.Environment.IsDevelopment())
{
    await OrderService.Infrastructure.Data.OrderSeeder.SeedAsync(app.Services);
}

app.UseSharedLogging();
app.UseObservability();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 7.2 health endpoints — /health (all), /health/ready (deps), /health/live (self)
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready"), ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live"), ResponseWriter = WriteHealthResponse });

// Configure the HTTP request pipeline.
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
