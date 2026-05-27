using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;

public static class TelemetryExtensions
{
    public const string DefaultOtlpEndpoint = "https://localhost:4317";

    /// <summary>
    /// Configures OpenTelemetry tracing, metrics, and logs for the demo apps,
    /// exporting via OTLP/gRPC to the standalone Aspire Dashboard.
    ///
    /// Sets <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> and
    /// <c>OTEL_EXPORTER_OTLP_PROTOCOL</c> in configuration if they are not
    /// already set, then registers a single OTLP exporter via
    /// <see cref="OpenTelemetryBuilderOtlpExporterExtensions.UseOtlpExporter(IOpenTelemetryBuilder)"/>
    /// so traces/metrics/logs all share one endpoint configuration.
    ///
    /// For MAUI, also call
    /// <see cref="EnsureDemoTelemetryStarted(IServiceProvider)"/> after
    /// <c>builder.Build()</c> (or register
    /// <c>OpenTelemetryInitializer</c> as an
    /// <c>IMauiInitializeService</c>) — the MAUI host does not start
    /// <see cref="IHostedService"/> instances, so the OTel providers are never
    /// otherwise resolved.
    /// </summary>
    public static TBuilder AddDemoTelemetry<TBuilder>(
        this TBuilder builder,
        string serviceName,
        string? otlpEndpoint = null,
        Action<TracerProviderBuilder>? configureTracing = null)
        where TBuilder : IHostApplicationBuilder
    {
        var endpoint = otlpEndpoint ?? DefaultOtlpEndpoint;

        // UseOtlpExporter() reads OTEL_EXPORTER_OTLP_ENDPOINT from configuration;
        // seed it (and force gRPC) without overwriting any caller-supplied value.
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ??= endpoint;
        builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] ??= "grpc";

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource("Microsoft.Extensions.AI")
                    .AddSource("Microsoft.Agents.AI")
                    .AddSource(serviceName);

                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddHttpClientInstrumentation()
                    .AddMeter("Microsoft.Extensions.AI")
                    .AddMeter("Microsoft.Agents.AI")
                    .AddMeter(serviceName);
            })
            .UseOtlpExporter();

        return builder;
    }

    /// <summary>
    /// Forces the OpenTelemetry TracerProvider, MeterProvider, and
    /// LoggerProvider to be built and their ActivitySource / Meter / log
    /// listeners to attach. Required for hosts that do not start
    /// <see cref="IHostedService"/> instances (notably .NET MAUI, which builds
    /// the host but never calls RunAsync). Without this, nothing is ever
    /// exported because the providers are never resolved from DI. Safe to call
    /// multiple times.
    /// </summary>
    public static IServiceProvider EnsureDemoTelemetryStarted(this IServiceProvider services)
    {
        _ = services.GetService(typeof(TracerProvider));
        _ = services.GetService(typeof(MeterProvider));
        _ = services.GetService(typeof(LoggerProvider));
        _ = services.GetService(typeof(ILoggerFactory));
        return services;
    }
}
