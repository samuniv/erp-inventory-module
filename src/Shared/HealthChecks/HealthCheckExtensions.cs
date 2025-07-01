using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using System.Net.Mime;

namespace Shared.HealthChecks;

/// <summary>
/// Extension methods for configuring comprehensive health checks
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds comprehensive health checks with custom checks for each service type
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="serviceName">Name of the service for health check identification</param>
    /// <param name="configureHealthChecks">Optional additional health check configuration</param>
    /// <returns>Health check builder for further configuration</returns>
    public static IServiceCollection AddComprehensiveHealthChecks(
        this IServiceCollection services,
        string serviceName,
        Action<IHealthChecksBuilder>? configureHealthChecks = null)
    {
        var healthChecksBuilder = services.AddHealthChecks()
            .AddCheck<ServiceHealthCheck>(
                name: $"{serviceName}_service_health",
                tags: ["service", "ready"])
            .AddCheck<MemoryHealthCheck>(
                name: $"{serviceName}_memory_usage",
                tags: ["memory", "ready"])
            .AddCheck<DiskSpaceHealthCheck>(
                name: $"{serviceName}_disk_space",
                tags: ["disk", "ready"]);

        // Add additional health checks if provided
        configureHealthChecks?.Invoke(healthChecksBuilder);

        return services;
    }

    /// <summary>
    /// Maps comprehensive health check endpoints with different detail levels
    /// </summary>
    public static IEndpointRouteBuilder MapComprehensiveHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Basic health check endpoint (minimal response)
        endpoints.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = WriteMinimalHealthCheckResponse,
            AllowCachingResponses = false,
            Predicate = _ => true
        });

        // Detailed health check endpoint with full information
        endpoints.MapHealthChecks("/health/detailed", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = WriteDetailedHealthCheckResponse,
            AllowCachingResponses = false,
            Predicate = _ => true
        });

        // Ready endpoint for Kubernetes readiness probes
        endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = WriteMinimalHealthCheckResponse,
            AllowCachingResponses = false,
            Predicate = healthCheck => healthCheck.Tags.Contains("ready")
        });

        // Live endpoint for Kubernetes liveness probes (essential checks only)
        endpoints.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = WriteMinimalHealthCheckResponse,
            AllowCachingResponses = false,
            Predicate = healthCheck => healthCheck.Tags.Contains("live")
        });

        return endpoints;
    }

    /// <summary>
    /// Writes a minimal health check response (for Kubernetes probes)
    /// </summary>
    private static async Task WriteMinimalHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;
        context.Response.StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503;

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    /// <summary>
    /// Writes a detailed health check response with all check results
    /// </summary>
    private static async Task WriteDetailedHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;
        context.Response.StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503;

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
                exception = entry.Value.Exception?.Message,
                data = entry.Value.Data.Count > 0 ? entry.Value.Data : null,
                tags = entry.Value.Tags
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
}

/// <summary>
/// Custom health check for service-specific health
/// </summary>
public class ServiceHealthCheck : IHealthCheck
{
    private readonly IHostEnvironment _environment;

    public ServiceHealthCheck(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["environment"] = _environment.EnvironmentName,
                ["contentRoot"] = _environment.ContentRootPath,
                ["timestamp"] = DateTimeOffset.UtcNow
            };

            return Task.FromResult(HealthCheckResult.Healthy("Service is running normally", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Service health check failed", ex));
        }
    }
}

/// <summary>
/// Custom health check for memory usage monitoring
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private const long UnhealthyThresholdBytes = 1024L * 1024L * 1024L; // 1 GB
    private const long DegradedThresholdBytes = 512L * 1024L * 1024L;   // 512 MB

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var memoryUsed = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;

            var data = new Dictionary<string, object>
            {
                ["memoryUsed"] = memoryUsed,
                ["workingSet"] = workingSet,
                ["memoryUsedMB"] = memoryUsed / (1024.0 * 1024.0),
                ["workingSetMB"] = workingSet / (1024.0 * 1024.0),
                ["generation0Collections"] = GC.CollectionCount(0),
                ["generation1Collections"] = GC.CollectionCount(1),
                ["generation2Collections"] = GC.CollectionCount(2)
            };

            if (memoryUsed > UnhealthyThresholdBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Memory usage is too high: {memoryUsed / (1024.0 * 1024.0):F2} MB",
                    data: data));
            }

            if (memoryUsed > DegradedThresholdBytes)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Memory usage is elevated: {memoryUsed / (1024.0 * 1024.0):F2} MB",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Memory usage is normal: {memoryUsed / (1024.0 * 1024.0):F2} MB",
                data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Memory health check failed", ex));
        }
    }
}

/// <summary>
/// Custom health check for disk space monitoring
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var drive = new DriveInfo(Path.GetPathRoot(currentDirectory) ?? "C:\\");

            var freeSpaceBytes = drive.AvailableFreeSpace;
            var totalSpaceBytes = drive.TotalSize;
            var usedSpaceBytes = totalSpaceBytes - freeSpaceBytes;
            var freeSpacePercentage = (double)freeSpaceBytes / totalSpaceBytes * 100;

            var data = new Dictionary<string, object>
            {
                ["drive"] = drive.Name,
                ["freeSpaceBytes"] = freeSpaceBytes,
                ["totalSpaceBytes"] = totalSpaceBytes,
                ["usedSpaceBytes"] = usedSpaceBytes,
                ["freeSpaceGB"] = freeSpaceBytes / (1024.0 * 1024.0 * 1024.0),
                ["totalSpaceGB"] = totalSpaceBytes / (1024.0 * 1024.0 * 1024.0),
                ["usedSpaceGB"] = usedSpaceBytes / (1024.0 * 1024.0 * 1024.0),
                ["freeSpacePercentage"] = freeSpacePercentage
            };

            if (freeSpacePercentage < 5) // Less than 5% free space
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Disk space critically low: {freeSpacePercentage:F2}% free",
                    data: data));
            }

            if (freeSpacePercentage < 15) // Less than 15% free space
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Disk space is low: {freeSpacePercentage:F2}% free",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space is adequate: {freeSpacePercentage:F2}% free",
                data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Disk space health check failed", ex));
        }
    }
}

/// <summary>
/// Health check for database connectivity (can be used with any Entity Framework DbContext)
/// </summary>
public class DatabaseHealthCheck<TDbContext> : IHealthCheck
    where TDbContext : class
{
    private readonly TDbContext _dbContext;

    public DatabaseHealthCheck(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // For Entity Framework DbContext
            if (_dbContext is Microsoft.EntityFrameworkCore.DbContext efContext)
            {
                var canConnect = await efContext.Database.CanConnectAsync(cancellationToken);

                if (!canConnect)
                {
                    return HealthCheckResult.Unhealthy("Cannot connect to database");
                }

                var data = new Dictionary<string, object>
                {
                    ["databaseProvider"] = efContext.Database.ProviderName ?? "Unknown",
                    ["canConnect"] = true
                };

                return HealthCheckResult.Healthy("Database connection is healthy", data);
            }

            return HealthCheckResult.Healthy("Database context is available");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }
}

/// <summary>
/// Health check for HTTP service dependencies
/// </summary>
public class HttpServiceHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceName;
    private readonly string _healthEndpoint;

    public HttpServiceHealthCheck(HttpClient httpClient, string serviceName, string healthEndpoint = "/health")
    {
        _httpClient = httpClient;
        _serviceName = serviceName;
        _healthEndpoint = healthEndpoint;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(_healthEndpoint, cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["service"] = _serviceName,
                ["endpoint"] = _healthEndpoint,
                ["statusCode"] = (int)response.StatusCode,
                ["responseTime"] = DateTimeOffset.UtcNow
            };

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"{_serviceName} is healthy", data);
            }

            return HealthCheckResult.Unhealthy($"{_serviceName} returned {response.StatusCode}", data: data);
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy($"{_serviceName} is unreachable", ex);
        }
        catch (TaskCanceledException ex)
        {
            return HealthCheckResult.Unhealthy($"{_serviceName} health check timed out", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{_serviceName} health check failed", ex);
        }
    }
}
