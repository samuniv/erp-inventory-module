using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Shared.Observability;

/// <summary>
/// Centralized OpenTelemetry configuration for all microservices
/// </summary>
public static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Adds OpenTelemetry with standardized configuration to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="serviceVersion">Version of the service</param>
    /// <param name="environment">Environment name</param>
    /// <param name="configureTracing">Optional additional tracing configuration</param>
    /// <param name="configureMetrics">Optional additional metrics configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0",
        string? environment = null,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        environment ??= Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.namespace"] = "ERP.InventoryModule",
                    ["service.instance.id"] = Environment.MachineName,
                    ["deployment.environment"] = environment
                }))
            .WithTracing(tracing =>
            {
                ConfigureBaseTracing(tracing);
                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                ConfigureBaseMetrics(metrics, serviceName);
                configureMetrics?.Invoke(metrics);
            });

        return services;
    }

    /// <summary>
    /// Configures base tracing instrumentation common to all services
    /// </summary>
    private static void ConfigureBaseTracing(TracerProviderBuilder tracing)
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpContext) =>
                {
                    // Skip health check and metrics endpoints from tracing
                    var path = httpContext.Request.Path.Value?.ToLowerInvariant();
                    return path != null && 
                           !path.StartsWith("/health") && 
                           !path.StartsWith("/metrics");
                };
                options.EnrichWithHttpRequest = (activity, httpRequest) =>
                {
                    activity.SetTag("http.request.size", httpRequest.ContentLength);
                    activity.SetTag("http.user_agent", httpRequest.Headers.UserAgent.ToString());
                };
                options.EnrichWithHttpResponse = (activity, httpResponse) =>
                {
                    activity.SetTag("http.response.size", httpResponse.ContentLength);
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.FilterHttpRequestMessage = (httpRequestMessage) => true;
                options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                {
                    activity.SetTag("http.request.method", httpRequestMessage.Method.Method);
                    activity.SetTag("http.request.uri", httpRequestMessage.RequestUri?.ToString());
                };
                options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                {
                    activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                    activity.SetTag("http.response.content_length", httpResponseMessage.Content.Headers.ContentLength);
                };
            })
            .SetSampler(GetSampler())
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(GetOtlpEndpoint());
                options.Protocol = GetOtlpProtocol();
            });
    }

    /// <summary>
    /// Configures base metrics collection common to all services
    /// </summary>
    private static void ConfigureBaseMetrics(MeterProviderBuilder metrics, string serviceName)
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter(serviceName)
            .AddPrometheusExporter()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(GetOtlpEndpoint());
                options.Protocol = GetOtlpProtocol();
            });
    }

    /// <summary>
    /// Gets the sampling strategy based on environment configuration
    /// </summary>
    private static Sampler GetSampler()
    {
        var samplingRatio = Environment.GetEnvironmentVariable("OTEL_TRACES_SAMPLER_ARG");
        
        if (double.TryParse(samplingRatio, out var ratio) && ratio >= 0 && ratio <= 1)
        {
            return new TraceIdRatioBasedSampler(ratio);
        }

        // Default sampling strategies
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return environment?.ToLowerInvariant() switch
        {
            "development" => new AlwaysOnSampler(),
            "staging" => new TraceIdRatioBasedSampler(0.1), // 10% sampling
            "production" => new TraceIdRatioBasedSampler(0.01), // 1% sampling
            _ => new AlwaysOnSampler()
        };
    }

    /// <summary>
    /// Gets the OTLP endpoint from environment configuration
    /// </summary>
    private static string GetOtlpEndpoint()
    {
        return Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? 
               Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? 
               "http://localhost:4317";
    }

    /// <summary>
    /// Gets the OTLP protocol from environment configuration
    /// </summary>
    private static OpenTelemetry.Exporter.OtlpExportProtocol GetOtlpProtocol()
    {
        var protocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        
        return protocol?.ToLowerInvariant() switch
        {
            "grpc" => OpenTelemetry.Exporter.OtlpExportProtocol.Grpc,
            "http/protobuf" => OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf,
            _ => OpenTelemetry.Exporter.OtlpExportProtocol.Grpc // Default to gRPC
        };
    }
}

/// <summary>
/// Extension methods for adding observability middleware
/// </summary>
public static class ObservabilityMiddlewareExtensions
{
    /// <summary>
    /// Adds custom observability middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseObservabilityMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ObservabilityMiddleware>();
    }
}

/// <summary>
/// Middleware for adding custom observability enrichment
/// </summary>
public class ObservabilityMiddleware
{
    private readonly RequestDelegate _next;

    public ObservabilityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var activity = ObservabilityActivitySource.Instance.StartActivity("http.request");
        
        // Enrich the activity with additional context
        activity?.SetTag("http.request.id", context.TraceIdentifier);
        activity?.SetTag("http.request.scheme", context.Request.Scheme);
        activity?.SetTag("http.request.host", context.Request.Host.Value);
        
        // Add correlation ID if available
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            activity?.SetTag("correlation.id", correlationId.ToString());
        }

        try
        {
            await _next(context);
            
            activity?.SetTag("http.response.status_code", context.Response.StatusCode);
            activity?.SetStatus(GetActivityStatus(context.Response.StatusCode));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private static ActivityStatusCode GetActivityStatus(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 400 => ActivityStatusCode.Ok,
            >= 400 and < 500 => ActivityStatusCode.Error,
            >= 500 => ActivityStatusCode.Error,
            _ => ActivityStatusCode.Unset
        };
    }
}

/// <summary>
/// Centralized activity source for the application
/// </summary>
public static class ObservabilityActivitySource
{
    public static readonly ActivitySource Instance = new("ERP.InventoryModule", "1.0.0");
}
