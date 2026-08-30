using BuildingBlocks.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// Add Yarp Reverse Proxy services by reading config from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// One line to activate the Correlation ID middleware at the start of the pipeline
app.UseSharedLogging();

app.UseRouting();

// Establish mapping rules for routing incoming client calls straight to target destination microservices
app.MapReverseProxy();

try
{
    Log.Information("Starting API Gateway application layer...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway startup phase terminated unexpectedly");
}
finally
{
    // Guarantee that all buffered diagnostic logs are fully written out before application shuts down
    Log.CloseAndFlush();
}
