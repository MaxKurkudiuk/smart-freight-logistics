using BuildingBlocks.EventBus;
using BuildingBlocks.EventBus.Extensions;
using BuildingBlocks.Logging;
using IntegrationService.Clients;
using IntegrationService.Consumers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// 4.6-4.8 No DB — stateless bridge, consumer registered (MassTransit Retry 3×1s via EventBus)
builder.AddEventBus(x => x.AddConsumer<OrderCreatedConsumer>());

// 7.2 HealthChecks — rabbitmq readiness + self liveness (replaces ad-hoc /health MapGet).
// HealthChecks.Rabbitmq 9.x takes the caller-provided IConnection (long-lived singleton per RabbitMQ guidance).
var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqSettings>() ?? new RabbitMqSettings();
var rabbitUri = new Uri($"amqp://{rabbit.User}:{rabbit.Password}@{rabbit.Host}:{rabbit.Port}{rabbit.VHost}");
builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory { Uri = rabbitUri };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});
builder.Services.AddHealthChecks()
    .AddRabbitMQ(name: "rabbitmq", tags: new[] { "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

// 4.8 HttpClient + Polly WaitAndRetry 3×2^retry + CircuitBreaker 5/30s
builder.Services.AddHttpClient<IRpaClient, RpaClient>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["Rpa:BaseUrl"] ?? "http://localhost:5004";
    http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry))))
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

builder.Services.AddHttpClient<IOrderStatusClient, OrderStatusClient>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["OrderService:BaseUrl"] ?? "http://localhost:5002";
    http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry))))
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

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

public partial class Program { }
