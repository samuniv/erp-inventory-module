using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Auth.Service.Observability;

/// <summary>
/// Custom metrics for Auth Service
/// </summary>
public class AuthMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _loginAttemptsCounter;
    private readonly Counter<long> _loginSuccessCounter;
    private readonly Counter<long> _loginFailureCounter;
    private readonly Counter<long> _registrationAttemptsCounter;
    private readonly Counter<long> _registrationSuccessCounter;
    private readonly Counter<long> _registrationFailureCounter;
    private readonly Counter<long> _tokenRefreshCounter;
    private readonly Counter<long> _tokenRevocationCounter;
    private readonly Counter<long> _passwordResetRequestsCounter;
    private readonly Counter<long> _passwordResetSuccessCounter;
    private readonly Histogram<double> _authenticationDurationHistogram;
    private readonly Histogram<double> _tokenGenerationDurationHistogram;
    private readonly UpDownCounter<long> _activeSessionsGauge;
    private readonly UpDownCounter<long> _lockedAccountsGauge;

    public AuthMetrics()
    {
        _meter = new Meter("Auth.Service", "1.0.0");

        // Authentication counters
        _loginAttemptsCounter = _meter.CreateCounter<long>(
            "login_attempts_total",
            description: "Total number of login attempts");

        _loginSuccessCounter = _meter.CreateCounter<long>(
            "login_success_total",
            description: "Total number of successful logins");

        _loginFailureCounter = _meter.CreateCounter<long>(
            "login_failure_total",
            description: "Total number of failed login attempts");

        // Registration counters
        _registrationAttemptsCounter = _meter.CreateCounter<long>(
            "registration_attempts_total",
            description: "Total number of registration attempts");

        _registrationSuccessCounter = _meter.CreateCounter<long>(
            "registration_success_total",
            description: "Total number of successful registrations");

        _registrationFailureCounter = _meter.CreateCounter<long>(
            "registration_failure_total",
            description: "Total number of failed registration attempts");

        // Token management counters
        _tokenRefreshCounter = _meter.CreateCounter<long>(
            "token_refresh_total",
            description: "Total number of token refresh operations");

        _tokenRevocationCounter = _meter.CreateCounter<long>(
            "token_revocation_total",
            description: "Total number of token revocation operations");

        // Password reset counters
        _passwordResetRequestsCounter = _meter.CreateCounter<long>(
            "password_reset_requests_total",
            description: "Total number of password reset requests");

        _passwordResetSuccessCounter = _meter.CreateCounter<long>(
            "password_reset_success_total",
            description: "Total number of successful password resets");

        // Duration histograms
        _authenticationDurationHistogram = _meter.CreateHistogram<double>(
            "authentication_duration_seconds",
            unit: "s",
            description: "Duration of authentication operations");

        _tokenGenerationDurationHistogram = _meter.CreateHistogram<double>(
            "token_generation_duration_seconds",
            unit: "s",
            description: "Duration of token generation operations");

        // State gauges
        _activeSessionsGauge = _meter.CreateUpDownCounter<long>(
            "active_sessions_current",
            description: "Current number of active user sessions");

        _lockedAccountsGauge = _meter.CreateUpDownCounter<long>(
            "locked_accounts_current",
            description: "Current number of locked user accounts");
    }

    /// <summary>
    /// Records a login attempt
    /// </summary>
    /// <param name="username">Username attempting login</param>
    /// <param name="success">Whether the login was successful</param>
    /// <param name="failureReason">Reason for failure (if applicable)</param>
    /// <param name="userAgent">User agent string</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="duration">Time taken for authentication</param>
    public void RecordLoginAttempt(string username, bool success, string? failureReason, string? userAgent, string? ipAddress, TimeSpan duration)
    {
        var tags = new TagList
        {
            {"username_hash", GetUsernameHash(username)},
            {"success", success.ToString().ToLower()},
            {"user_agent_type", GetUserAgentType(userAgent)},
            {"ip_range", GetIpRange(ipAddress)},
            {"duration_range", GetDurationRange(duration)}
        };

        if (!success && !string.IsNullOrEmpty(failureReason))
        {
            tags.Add("failure_reason", failureReason);
        }

        _loginAttemptsCounter.Add(1, tags);
        _authenticationDurationHistogram.Record(duration.TotalSeconds, tags);

        if (success)
        {
            _loginSuccessCounter.Add(1, tags);
            _activeSessionsGauge.Add(1);
        }
        else
        {
            _loginFailureCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Records a registration attempt
    /// </summary>
    /// <param name="username">Username being registered</param>
    /// <param name="success">Whether the registration was successful</param>
    /// <param name="failureReason">Reason for failure (if applicable)</param>
    /// <param name="userAgent">User agent string</param>
    /// <param name="ipAddress">Client IP address</param>
    public void RecordRegistrationAttempt(string username, bool success, string? failureReason, string? userAgent, string? ipAddress)
    {
        var tags = new TagList
        {
            {"username_hash", GetUsernameHash(username)},
            {"success", success.ToString().ToLower()},
            {"user_agent_type", GetUserAgentType(userAgent)},
            {"ip_range", GetIpRange(ipAddress)}
        };

        if (!success && !string.IsNullOrEmpty(failureReason))
        {
            tags.Add("failure_reason", failureReason);
        }

        _registrationAttemptsCounter.Add(1, tags);

        if (success)
        {
            _registrationSuccessCounter.Add(1, tags);
        }
        else
        {
            _registrationFailureCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Records token refresh operation
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="success">Whether the refresh was successful</param>
    /// <param name="tokenType">Type of token being refreshed</param>
    /// <param name="duration">Time taken for token generation</param>
    public void RecordTokenRefresh(string userId, bool success, string tokenType, TimeSpan duration)
    {
        var tags = new TagList
        {
            {"user_id_hash", GetUserIdHash(userId)},
            {"token_type", tokenType},
            {"success", success.ToString().ToLower()},
            {"duration_range", GetDurationRange(duration)}
        };

        _tokenRefreshCounter.Add(1, tags);
        _tokenGenerationDurationHistogram.Record(duration.TotalSeconds, tags);
    }

    /// <summary>
    /// Records token revocation
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tokenType">Type of token being revoked</param>
    /// <param name="reason">Reason for revocation</param>
    public void RecordTokenRevocation(string userId, string tokenType, string reason)
    {
        var tags = new TagList
        {
            {"user_id_hash", GetUserIdHash(userId)},
            {"token_type", tokenType},
            {"revocation_reason", reason}
        };

        _tokenRevocationCounter.Add(1, tags);
        _activeSessionsGauge.Add(-1);
    }

    /// <summary>
    /// Records password reset request
    /// </summary>
    /// <param name="username">Username requesting password reset</param>
    /// <param name="success">Whether the reset was initiated successfully</param>
    /// <param name="ipAddress">Client IP address</param>
    public void RecordPasswordResetRequest(string username, bool success, string? ipAddress)
    {
        var tags = new TagList
        {
            {"username_hash", GetUsernameHash(username)},
            {"success", success.ToString().ToLower()},
            {"ip_range", GetIpRange(ipAddress)}
        };

        _passwordResetRequestsCounter.Add(1, tags);
    }

    /// <summary>
    /// Records successful password reset completion
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="ipAddress">Client IP address</param>
    public void RecordPasswordResetSuccess(string userId, string? ipAddress)
    {
        var tags = new TagList
        {
            {"user_id_hash", GetUserIdHash(userId)},
            {"ip_range", GetIpRange(ipAddress)}
        };

        _passwordResetSuccessCounter.Add(1, tags);
    }

    /// <summary>
    /// Records account lockout event
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="reason">Reason for lockout</param>
    /// <param name="duration">Lockout duration</param>
    public void RecordAccountLockout(string userId, string reason, TimeSpan duration)
    {
        var lockoutCounter = _meter.CreateCounter<long>(
            "account_lockouts_total",
            description: "Total number of account lockout events");

        var tags = new TagList
        {
            {"user_id_hash", GetUserIdHash(userId)},
            {"lockout_reason", reason},
            {"lockout_duration_range", GetDurationRange(duration)}
        };

        lockoutCounter.Add(1, tags);
        _lockedAccountsGauge.Add(1);
    }

    /// <summary>
    /// Records account unlock event
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="unlockMethod">Method used to unlock (automatic, manual, etc.)</param>
    public void RecordAccountUnlock(string userId, string unlockMethod)
    {
        var unlockCounter = _meter.CreateCounter<long>(
            "account_unlocks_total",
            description: "Total number of account unlock events");

        var tags = new TagList
        {
            {"user_id_hash", GetUserIdHash(userId)},
            {"unlock_method", unlockMethod}
        };

        unlockCounter.Add(1, tags);
        _lockedAccountsGauge.Add(-1);
    }

    /// <summary>
    /// Records security event (suspicious activity, rate limiting, etc.)
    /// </summary>
    /// <param name="eventType">Type of security event</param>
    /// <param name="severity">Severity level</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">User agent string</param>
    public void RecordSecurityEvent(string eventType, string severity, string? ipAddress, string? userAgent)
    {
        var securityEventCounter = _meter.CreateCounter<long>(
            "security_events_total",
            description: "Total number of security events detected");

        var tags = new TagList
        {
            {"event_type", eventType},
            {"severity", severity},
            {"ip_range", GetIpRange(ipAddress)},
            {"user_agent_type", GetUserAgentType(userAgent)}
        };

        securityEventCounter.Add(1, tags);
    }

    /// <summary>
    /// Gets a hash of the username for privacy-preserving metrics
    /// </summary>
    private static string GetUsernameHash(string username)
    {
        return Math.Abs(username.GetHashCode()).ToString()[..6];
    }

    /// <summary>
    /// Gets a hash of the user ID for privacy-preserving metrics
    /// </summary>
    private static string GetUserIdHash(string userId)
    {
        return Math.Abs(userId.GetHashCode()).ToString()[..6];
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
            _ when ua.Contains("chrome") => "desktop_browser",
            _ when ua.Contains("firefox") => "desktop_browser",
            _ when ua.Contains("safari") => "desktop_browser",
            _ when ua.Contains("edge") => "desktop_browser",
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
            _ => "very_slow"
        };
    }

    public void Dispose()
    {
        _meter?.Dispose();
        GC.SuppressFinalize(this);
    }
}
