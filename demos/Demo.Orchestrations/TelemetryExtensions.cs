using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Demo.Orchestrations;

public static class TelemetryExtensions
{
    public const string DefaultOtlpEndpoint = "http://localhost:4317";

    /// <summary>
    /// Adds OpenTelemetry tracing, metrics, and log export via OTLP/gRPC
    /// to the standalone Aspire Dashboard.
    /// </summary>
    public static IServiceCollection AddDemoTelemetry(
        this IServiceCollection services,
        string serviceName,
        string? otlpEndpoint = null,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        var endpoint = otlpEndpoint ?? DefaultOtlpEndpoint;
        var endpointUri = new Uri(endpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource("Microsoft.Extensions.AI")
                    .AddSource("Microsoft.Agents.AI")
                    .AddSource(serviceName)
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = endpointUri;
                        o.Protocol = OtlpExportProtocol.Grpc;
                    });

                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddHttpClientInstrumentation()
                    .AddMeter("Microsoft.Extensions.AI")
                    .AddMeter("Microsoft.Agents.AI")
                    .AddMeter(serviceName)
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = endpointUri;
                        o.Protocol = OtlpExportProtocol.Grpc;
                    });
            });

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(otel =>
            {
                otel.IncludeFormattedMessage = true;
                otel.IncludeScopes = true;
                otel.AddOtlpExporter(o =>
                {
                    o.Endpoint = endpointUri;
                    o.Protocol = OtlpExportProtocol.Grpc;
                });
            });
        });

        return services;
    }
}
