using BuildingBlocks.EventBus.Extensions;
using BuildingBlocks.Logging;
using IntegrationService.Clients;
using IntegrationService.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// 4.6-4.7 No DB — stateless bridge, consumer registered (MassTransit Retry 3×1s via EventBus)
builder.AddEventBus(x => x.AddConsumer<OrderCreatedConsumer>());

// 4.7 stubs — 4.8 will replace with AddHttpClient<RpaClient> + Polly
builder.Services.AddScoped<IRpaClient, RpaClient>();
builder.Services.AddScoped<IOrderStatusClient, OrderStatusClient>();
builder.Services.AddHttpClient("rpa");
builder.Services.AddHttpClient("order-status");

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
