using Serilog;
using Serilog.Enrichers.CorrelationId;
using Serilog.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Shared.Logging;

/// <summary>
/// Centralized logging configuration for all microservices
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog with standardized settings for all services
    /// </summary>
    /// <param name="serviceName">Name of the service for identification in logs</param>
    /// <param name="configuration">Optional configuration to override default settings</param>
    /// <returns>Configured logger</returns>
    public static ILogger CreateLogger(string serviceName, IConfiguration? configuration = null)
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("Application", "ERP.InventoryModule");

        // Configure sinks
        ConfigureConsoleSink(loggerConfig);
        ConfigureFileSink(loggerConfig, serviceName);
        ConfigureSeqSink(loggerConfig);

        // Apply configuration overrides if provided
        if (configuration != null)
        {
            loggerConfig.ReadFrom.Configuration(configuration);
        }

        return loggerConfig.CreateLogger();
    }

    /// <summary>
    /// Configures the console sink with structured output
    /// </summary>
    private static void ConfigureConsoleSink(LoggerConfiguration loggerConfig)
    {
        loggerConfig.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }

    /// <summary>
    /// Configures the file sink with rolling policies
    /// </summary>
    private static void ConfigureFileSink(LoggerConfiguration loggerConfig, string serviceName)
    {
        var logFileName = serviceName.ToLowerInvariant().Replace(".", "-");

        loggerConfig.WriteTo.File(
            path: $"logs/{logFileName}-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            fileSizeLimitBytes: 10_000_000,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }

    /// <summary>
    /// Configures the Seq sink for centralized logging
    /// </summary>
    private static void ConfigureSeqSink(LoggerConfiguration loggerConfig)
    {
        var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341";

        loggerConfig.WriteTo.Seq(seqUrl);
    }

    /// <summary>
    /// Creates a logger with environment-specific configuration
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="environment">Current environment (Development, Production, etc.)</param>
    /// <returns>Configured logger</returns>
    public static ILogger CreateEnvironmentLogger(string serviceName, string environment)
    {
        var loggerConfig = new LoggerConfiguration()
            .Enrich.WithProperty("Environment", environment);

        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            // More verbose logging in development
            loggerConfig
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information);
        }
        else if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            // More conservative logging in production
            loggerConfig
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
        }

        return CreateLogger(serviceName);
    }
}

/// <summary>
/// Extension methods for adding structured logging to services
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds structured logging with correlation ID tracking
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services, string serviceName)
    {
        services.AddHttpContextAccessor();

        services.AddSingleton<ILogger>(provider =>
        {
            var configuration = provider.GetService<IConfiguration>();
            return LoggingConfiguration.CreateLogger(serviceName, configuration);
        });

        return services;
    }

    /// <summary>
    /// Adds request/response logging middleware
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}

/// <summary>
/// Middleware for logging HTTP requests and responses
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Log request
        _logger.Information("HTTP {Method} {Path} started",
            context.Request.Method,
            context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            _logger.Information("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}

/// <summary>
/// Structured logging templates for common scenarios
/// </summary>
public static class LoggingTemplates
{
    public const string DatabaseOperation = "Database operation {Operation} on {Entity} completed in {ElapsedMilliseconds}ms";
    public const string ApiCall = "API call to {ServiceName}.{Method} completed with status {StatusCode} in {ElapsedMilliseconds}ms";
    public const string BusinessRule = "Business rule {RuleName} evaluated to {Result} for {Entity}";
    public const string UserAction = "User {UserId} performed {Action} on {Resource}";
    public const string SystemEvent = "System event {EventType} occurred for {Entity}";
    public const string ErrorOccurred = "Error occurred in {Operation}: {ErrorMessage}";
    public const string PerformanceMetric = "Performance metric {MetricName} recorded value {Value} for {Component}";
}

/// <summary>
/// Correlation ID management
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    public static string? Current
    {
        get => _correlationId.Value;
        set => _correlationId.Value = value;
    }

    public static string GetOrGenerate()
    {
        if (string.IsNullOrEmpty(Current))
        {
            Current = Guid.NewGuid().ToString("N")[..8];
        }
        return Current;
    }
}
