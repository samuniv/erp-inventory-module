using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Gateway.API.Observability;

/// <summary>
/// Custom metrics for Gateway API
/// </summary>
public class GatewayMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestsRoutedCounter;
    private readonly Counter<long> _routingFailuresCounter;
    private readonly Counter<long> _authenticationAttemptsCounter;
    private readonly Counter<long> _authenticationFailuresCounter;
    private readonly Counter<long> _rateLimitExceededCounter;
    private readonly Counter<long> _circuitBreakerOpenCounter;
    private readonly Histogram<double> _requestDurationHistogram;
    private readonly Histogram<double> _downstreamResponseTimeHistogram;
    private readonly UpDownCounter<long> _activeConnectionsGauge;
    private readonly UpDownCounter<long> _authenticatedSessionsGauge;

    public GatewayMetrics()
    {
        _meter = new Meter("Gateway.API", "1.0.0");

        // Request routing counters
        _requestsRoutedCounter = _meter.CreateCounter<long>(
            "requests_routed_total",
            description: "Total number of requests routed through the gateway");

        _routingFailuresCounter = _meter.CreateCounter<long>(
            "routing_failures_total",
            description: "Total number of routing failures");

        // Authentication counters
        _authenticationAttemptsCounter = _meter.CreateCounter<long>(
            "authentication_attempts_total",
            description: "Total number of authentication attempts at the gateway");

        _authenticationFailuresCounter = _meter.CreateCounter<long>(
            "authentication_failures_total",
            description: "Total number of authentication failures at the gateway");

        // Rate limiting and circuit breaker counters
        _rateLimitExceededCounter = _meter.CreateCounter<long>(
            "rate_limit_exceeded_total",
            description: "Total number of requests that exceeded rate limits");

        _circuitBreakerOpenCounter = _meter.CreateCounter<long>(
            "circuit_breaker_open_total",
            description: "Total number of times circuit breaker opened for downstream services");

        // Duration histograms
        _requestDurationHistogram = _meter.CreateHistogram<double>(
            "request_duration_seconds",
            unit: "s",
            description: "Duration of requests processed by the gateway");

        _downstreamResponseTimeHistogram = _meter.CreateHistogram<double>(
            "downstream_response_time_seconds",
            unit: "s",
            description: "Response time from downstream services");

        // Connection gauges
        _activeConnectionsGauge = _meter.CreateUpDownCounter<long>(
            "active_connections_current",
            description: "Current number of active connections to the gateway");

        _authenticatedSessionsGauge = _meter.CreateUpDownCounter<long>(
            "authenticated_sessions_current",
            description: "Current number of authenticated sessions");
    }

    /// <summary>
    /// Records a request being routed through the gateway
    /// </summary>
    /// <param name="route">Route taken</param>
    /// <param name="method">HTTP method</param>
    /// <param name="statusCode">Response status code</param>
    /// <param name="downstreamService">Target downstream service</param>
    /// <param name="duration">Total request duration</param>
    /// <param name="downstreamDuration">Time spent waiting for downstream service</param>
    /// <param name="isAuthenticated">Whether the request was authenticated</param>
    public void RecordRequestRouted(string route, string method, int statusCode, string downstreamService,
        TimeSpan duration, TimeSpan downstreamDuration, bool isAuthenticated)
    {
        var tags = new TagList
        {
            {"route", SanitizeRoute(route)},
            {"method", method},
            {"status_code", statusCode},
            {"status_class", GetStatusClass(statusCode)},
            {"downstream_service", downstreamService},
            {"is_authenticated", isAuthenticated.ToString().ToLower()},
            {"duration_range", GetDurationRange(duration)}
        };

        _requestsRoutedCounter.Add(1, tags);
        _requestDurationHistogram.Record(duration.TotalSeconds, tags);
        _downstreamResponseTimeHistogram.Record(downstreamDuration.TotalSeconds, tags);

        if (statusCode >= 400)
        {
            _routingFailuresCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Records authentication attempt at the gateway
    /// </summary>
    /// <param name="authMethod">Authentication method used</param>
    /// <param name="success">Whether authentication was successful</param>
    /// <param name="failureReason">Reason for failure (if applicable)</param>
    /// <param name="userAgent">User agent</param>
    /// <param name="ipAddress">Client IP address</param>
    public void RecordAuthenticationAttempt(string authMethod, bool success, string? failureReason,
        string? userAgent, string? ipAddress)
    {
        var tags = new TagList
        {
            {"auth_method", authMethod},
            {"success", success.ToString().ToLower()},
            {"user_agent_type", GetUserAgentType(userAgent)},
            {"ip_range", GetIpRange(ipAddress)}
        };

        if (!success && !string.IsNullOrEmpty(failureReason))
        {
            tags.Add("failure_reason", failureReason);
        }

        _authenticationAttemptsCounter.Add(1, tags);

        if (success)
        {
            _authenticatedSessionsGauge.Add(1);
        }
        else
        {
            _authenticationFailuresCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Records rate limit exceeded event
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="endpoint">Endpoint that was rate limited</param>
    /// <param name="limitType">Type of limit (per_minute, per_hour, etc.)</param>
    /// <param name="ipAddress">Client IP address</param>
    public void RecordRateLimitExceeded(string clientId, string endpoint, string limitType, string? ipAddress)
    {
        var tags = new TagList
        {
            {"client_id_hash", GetClientIdHash(clientId)},
            {"endpoint", SanitizeRoute(endpoint)},
            {"limit_type", limitType},
            {"ip_range", GetIpRange(ipAddress)}
        };

        _rateLimitExceededCounter.Add(1, tags);
    }

    /// <summary>
    /// Records circuit breaker opening for a downstream service
    /// </summary>
    /// <param name="downstreamService">Service for which circuit breaker opened</param>
    /// <param name="reason">Reason for opening</param>
    /// <param name="failureCount">Number of failures that triggered the opening</param>
    public void RecordCircuitBreakerOpen(string downstreamService, string reason, int failureCount)
    {
        var tags = new TagList
        {
            {"downstream_service", downstreamService},
            {"reason", reason},
            {"failure_count", failureCount},
            {"failure_count_range", GetFailureCountRange(failureCount)}
        };

        _circuitBreakerOpenCounter.Add(1, tags);
    }

    /// <summary>
    /// Records connection events
    /// </summary>
    /// <param name="connectionType">Type of connection (websocket, http, etc.)</param>
    /// <param name="connected">Whether connecting or disconnecting</param>
    /// <param name="duration">Duration of connection (for disconnections)</param>
    public void RecordConnectionEvent(string connectionType, bool connected, TimeSpan? duration = null)
    {
        var connectionEventCounter = _meter.CreateCounter<long>(
            "connection_events_total",
            description: "Total number of connection events");

        var tags = new TagList
        {
            {"connection_type", connectionType},
            {"event_type", connected ? "connected" : "disconnected"}
        };

        if (!connected && duration.HasValue)
        {
            tags.Add("session_duration_range", GetDurationRange(duration.Value));
        }

        connectionEventCounter.Add(1, tags);

        if (connected)
        {
            _activeConnectionsGauge.Add(1);
        }
        else
        {
            _activeConnectionsGauge.Add(-1);
        }
    }

    /// <summary>
    /// Records load balancing decision
    /// </summary>
    /// <param name="service">Service being load balanced</param>
    /// <param name="selectedInstance">Instance selected</param>
    /// <param name="algorithm">Load balancing algorithm used</param>
    /// <param name="availableInstances">Number of available instances</param>
    /// <param name="instanceHealth">Health status of selected instance</param>
    public void RecordLoadBalancingDecision(string service, string selectedInstance, string algorithm,
        int availableInstances, string instanceHealth)
    {
        var loadBalanceCounter = _meter.CreateCounter<long>(
            "load_balance_decisions_total",
            description: "Total number of load balancing decisions");

        var tags = new TagList
        {
            {"service", service},
            {"selected_instance", selectedInstance},
            {"algorithm", algorithm},
            {"available_instances", availableInstances},
            {"instance_health", instanceHealth}
        };

        loadBalanceCounter.Add(1, tags);
    }

    /// <summary>
    /// Records cache operation
    /// </summary>
    /// <param name="operation">Cache operation (hit, miss, set, evict)</param>
    /// <param name="cacheType">Type of cache (response, auth, etc.)</param>
    /// <param name="key">Cache key (sanitized)</param>
    public void RecordCacheOperation(string operation, string cacheType, string key)
    {
        var cacheOperationCounter = _meter.CreateCounter<long>(
            "cache_operations_total",
            description: "Total number of cache operations");

        var tags = new TagList
        {
            {"operation", operation},
            {"cache_type", cacheType},
            {"key_hash", GetKeyHash(key)}
        };

        cacheOperationCounter.Add(1, tags);
    }

    /// <summary>
    /// Records security event at the gateway level
    /// </summary>
    /// <param name="eventType">Type of security event</param>
    /// <param name="severity">Severity level</param>
    /// <param name="source">Source of the event</param>
    /// <param name="ipAddress">Client IP address</param>
    public void RecordSecurityEvent(string eventType, string severity, string source, string? ipAddress)
    {
        var securityEventCounter = _meter.CreateCounter<long>(
            "security_events_total",
            description: "Total number of security events at the gateway");

        var tags = new TagList
        {
            {"event_type", eventType},
            {"severity", severity},
            {"source", source},
            {"ip_range", GetIpRange(ipAddress)}
        };

        securityEventCounter.Add(1, tags);
    }

    /// <summary>
    /// Sanitizes route for metrics to avoid high cardinality
    /// </summary>
    private static string SanitizeRoute(string route)
    {
        // Replace dynamic route parameters with placeholders
        var sanitized = route.ToLowerInvariant();

        // Replace GUIDs
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", "{id}");

        // Replace numeric IDs
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"/\d+", "/{id}");

        return sanitized;
    }

    /// <summary>
    /// Gets HTTP status class
    /// </summary>
    private static string GetStatusClass(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            >= 400 and < 500 => "4xx",
            >= 500 => "5xx",
            _ => "other"
        };
    }

    /// <summary>
    /// Categorizes user agent into broad types
    /// </summary>
    private static string GetUserAgentType(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "unknown";

        var ua = userAgent.ToLowerInvariant();
        return ua switch
        {
            _ when ua.Contains("mobile") => "mobile",
            _ when ua.Contains("tablet") => "tablet",
            _ when ua.Contains("postman") => "api_client",
            _ when ua.Contains("curl") => "api_client",
            _ when ua.Contains("browser") => "browser",
            _ => "other"
        };
    }

    /// <summary>
    /// Categorizes IP addresses into ranges for privacy
    /// </summary>
    private static string GetIpRange(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return "unknown";

        return ipAddress switch
        {
            _ when ipAddress.StartsWith("127.") => "localhost",
            _ when ipAddress.StartsWith("192.168.") => "private_192",
            _ when ipAddress.StartsWith("10.") => "private_10",
            _ when ipAddress.StartsWith("172.") => "private_172",
            _ => "public"
        };
    }

    /// <summary>
    /// Categorizes duration into ranges
    /// </summary>
    private static string GetDurationRange(TimeSpan duration)
    {
        return duration.TotalMilliseconds switch
        {
            <= 100 => "very_fast",
            <= 500 => "fast",
            <= 1000 => "normal",
            <= 2000 => "slow",
            <= 5000 => "very_slow",
            _ => "timeout"
        };
    }

    /// <summary>
    /// Gets failure count range
    /// </summary>
    private static string GetFailureCountRange(int count)
    {
        return count switch
        {
            <= 5 => "low",
            <= 10 => "medium",
            <= 20 => "high",
            _ => "very_high"
        };
    }

    /// <summary>
    /// Gets a hash of the client ID for privacy-preserving metrics
    /// </summary>
    private static string GetClientIdHash(string clientId)
    {
        return Math.Abs(clientId.GetHashCode()).ToString()[..6];
    }

    /// <summary>
    /// Gets a hash of the cache key for privacy-preserving metrics
    /// </summary>
    private static string GetKeyHash(string key)
    {
        return Math.Abs(key.GetHashCode()).ToString()[..6];
    }

    public void Dispose()
    {
        _meter?.Dispose();
        GC.SuppressFinalize(this);
    }
}
