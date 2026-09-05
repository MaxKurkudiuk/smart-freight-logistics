using BuildingBlocks.EventBus.Extensions;
using BuildingBlocks.Logging;
using IntegrationService.Clients;
using IntegrationService.Consumers;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// 4.6-4.8 No DB — stateless bridge, consumer registered (MassTransit Retry 3×1s via EventBus)
builder.AddEventBus(x => x.AddConsumer<OrderCreatedConsumer>());

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "IntegrationService" }));

app.Run();

public partial class Program { }
