using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// 7.1 shared OpenTelemetry registration — resource + tracing (AspNetCore,
    /// HttpClient) + metrics (AspNetCore, Runtime) + logging.
    /// The OTLP exporter is added only when Observability:OtlpEndpoint is set;
    /// otherwise the pipeline is a no-op (console exporter when EnableConsole).
    /// Call from Program.cs: builder.AddObservability("OrderService").
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder, string serviceName)
    {
        var section = builder.Configuration.GetSection("Observability");
        var settings = new ObservabilitySettings
        {
            ServiceName = section["ServiceName"] ?? string.Empty,
            OtlpEndpoint = section["OtlpEndpoint"],
            EnableConsole = bool.TryParse(section["EnableConsole"], out var console) && console
        };
        if (string.IsNullOrWhiteSpace(settings.ServiceName))
            settings.ServiceName = serviceName;

        builder.Services.AddSingleton(settings);

        var hasOtlp = !string.IsNullOrWhiteSpace(settings.OtlpEndpoint);
        var otlpEndpoint = hasOtlp ? new Uri(settings.OtlpEndpoint!) : null;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(settings.ServiceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();
                if (otlpEndpoint is not null)
                    tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                else if (settings.EnableConsole)
                    tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddRuntimeInstrumentation();
                if (otlpEndpoint is not null)
                    metrics.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                else if (settings.EnableConsole)
                    metrics.AddConsoleExporter();
            })
            .WithLogging(logging =>
            {
                if (otlpEndpoint is not null)
                    logging.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
            });

        return builder;
    }
}
