using YarpGateway.Extensions;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();
builder.AddAuth();

// 7.3 OpenTelemetry — base AspNetCore/HttpClient (traceparent flows to downstream clusters)
builder.AddObservability("YarpGateway");

// 7.2 HealthChecks — self liveness (dependency health per cluster lands in 7.4)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

// Add Yarp Reverse Proxy services by reading config from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// One line to activate the Correlation ID middleware at the start of the pipeline
app.UseSharedLogging();
app.UseObservability();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 7.2 health endpoints — /health (all), /health/live (self)
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live"), ResponseWriter = WriteHealthResponse });

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

static async Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
    });
}
