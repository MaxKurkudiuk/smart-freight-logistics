using BuildingBlocks.EventBus.Extensions;
using BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// 4.6 No DB — stateless HttpClient bridge
builder.AddEventBus();

// HttpClient placeholders — real RpaClient / OrderStatusClient in 4.8
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
