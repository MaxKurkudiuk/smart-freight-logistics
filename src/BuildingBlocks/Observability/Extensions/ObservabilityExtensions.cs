using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
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
    /// 7.3 extra per-service ActivitySources/Meters via <paramref name="configure"/>.
    /// The OTLP exporter is added only when Observability:OtlpEndpoint is set;
    /// otherwise the pipeline is a no-op (console exporter when EnableConsole).
    /// Call from Program.cs: builder.AddObservability("OrderService", s => s.TraceSources.Add("Npgsql")).
    /// </summary>
    public static WebApplicationBuilder AddObservability(
        this WebApplicationBuilder builder,
        string serviceName,
        Action<ObservabilitySources>? configure = null)
    {
        var section = builder.Configuration.GetSection("Observability");
        var settings = new ObservabilitySettings
        {
            ServiceName = section["ServiceName"] ?? string.Empty,
            OtlpEndpoint = section["OtlpEndpoint"],
            EnableConsole = bool.TryParse(section["EnableConsole"], out var console) && console,
            EnablePrometheus = bool.TryParse(section["EnablePrometheus"], out var prometheus) && prometheus
        };
        if (string.IsNullOrWhiteSpace(settings.ServiceName))
            settings.ServiceName = serviceName;

        builder.Services.AddSingleton(settings);

        var sources = new ObservabilitySources();
        configure?.Invoke(sources);

        var hasOtlp = !string.IsNullOrWhiteSpace(settings.OtlpEndpoint);
        var otlpEndpoint = hasOtlp ? new Uri(settings.OtlpEndpoint!) : null;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(settings.ServiceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();
                foreach (var source in sources.TraceSources)
                    tracing.AddSource(source);
                if (otlpEndpoint is not null)
                    tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                else if (settings.EnableConsole)
                    tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddRuntimeInstrumentation();
                foreach (var meter in sources.MeterNames)
                    metrics.AddMeter(meter);
                if (settings.EnablePrometheus)
                    metrics.AddPrometheusExporter();
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

    /// <summary>
    /// 7.3 maps the Prometheus scrape endpoint (/metrics) when Observability:EnablePrometheus is set.
    /// Call from Program.cs after UseRouting: app.UseObservability().
    /// </summary>
    public static WebApplication UseObservability(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<ObservabilitySettings>();
        if (settings.EnablePrometheus)
            app.UseOpenTelemetryPrometheusScrapingEndpoint();
        return app;
    }
}
