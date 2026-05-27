using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Demo.MauiAgentApp;

/// <summary>
/// Single-call OpenTelemetry setup for the demo app.
///
/// Pattern lifted from the dotnet/maui Aspire ServiceDefaults template
/// (https://github.com/dotnet/maui/blob/main/src/Templates/src/templates/maui-aspire-servicedefaults/Extensions.cs)
/// and trimmed to what the demo actually needs.
///
/// Adds:
///   - tracing for HTTP client, Microsoft.Extensions.AI, Microsoft.Agents.AI
///   - metrics for HTTP client, runtime, Microsoft.Extensions.AI, Microsoft.Agents.AI
///   - log export via the OTel logging provider on <see cref="ILoggingBuilder"/>
///   - one <c>UseOtlpExporter()</c> for all three signals (reads
///     <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> / <c>_PROTOCOL</c> from configuration)
///   - an <see cref="IMauiInitializeService"/> that forces TracerProvider /
///     MeterProvider / LoggerProvider resolution after the host is built
///     (MAUI never starts IHostedService instances so the OTel hosted service
///     never runs on its own).
///
/// Defaults to <c>https://localhost:4317</c> because on Apple platforms the
/// default HttpClient handler (NSUrlSessionHandler / CFNetwork) has no support
/// for HTTP/2 over cleartext, and OTLP/gRPC requires HTTP/2.
/// </summary>
public static class TelemetryExtensions
{
    public const string DefaultOtlpEndpoint = "https://localhost:4317";

    /// <summary>
    /// Configures OpenTelemetry traces, metrics, and logs for the demo app
    /// and registers a MAUI initialize hook so providers actually start.
    /// Idempotent — safe to call from any <c>IHostApplicationBuilder</c>.
    /// </summary>
    public static TBuilder AddDemoTelemetry<TBuilder>(
        this TBuilder builder,
        string serviceName)
        where TBuilder : IHostApplicationBuilder
    {
        // Let .UseOtlpExporter() pick these up from configuration. Anything
        // the caller has already set (env var, appsettings, etc.) wins.
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ??= DefaultOtlpEndpoint;
        builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] ??= "grpc";

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation()
                .AddSource("Microsoft.Extensions.AI")
                .AddSource("Microsoft.Agents.AI")
                .AddSource(serviceName))
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Microsoft.Extensions.AI")
                .AddMeter("Microsoft.Agents.AI")
                .AddMeter(serviceName))
            .UseOtlpExporter();

        // MAUI doesn't start IHostedService instances, so the OpenTelemetry
        // hosted service never runs and the providers are never resolved
        // (so no ActivitySource / Meter / log listeners attach). This hook
        // runs after Build() on the UI thread with the full IServiceProvider.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMauiInitializeService, OpenTelemetryInitializer>());

        return builder;
    }

    private sealed class OpenTelemetryInitializer : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
        {
            _ = services.GetService<TracerProvider>();
            _ = services.GetService<MeterProvider>();
            _ = services.GetService<LoggerProvider>();
        }
    }
}
