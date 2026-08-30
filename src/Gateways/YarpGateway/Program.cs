using Serilog;
using YarpGateway;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog by reading setup parameters directly from appsettings.json configuration file
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// Force the host builder engine to substitute the built-in Microsoft logger with Serilog extension
builder.Host.UseSerilog();

// REGISTER SERVICES IN THE DI CONTAINER:
// Register the factory-based CorrelationIdMiddleware instance as a transient component
builder.Services.AddTransient<CorrelationIdMiddleware>();

// Add Yarp Reverse Proxy services by reading config from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// CONFIGURE THE HTTP REQUEST PIPELINE (MIDDLEWARES):
// Resolve and trigger the CorrelationIdMiddleware right at the entry point of the pipeline
app.UseMiddleware<CorrelationIdMiddleware>();

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
