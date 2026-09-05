using BuildingBlocks.EventBus.Extensions;
using BuildingBlocks.Logging;
using MassTransit;
using OrderService.API.Extensions;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

builder.AddOrderDbContext();
builder.AddOrderAuth();
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
builder.Services.AddScoped<IOrderService, OrderService.Application.Services.OrderService>();

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

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

public partial class Program { }
