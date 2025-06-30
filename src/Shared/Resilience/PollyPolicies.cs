using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.CircuitBreaker;
using System.Net;

namespace Shared.Resilience;

/// <summary>
/// Static factory class for creating standard Polly resilience policies
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Creates a combined HTTP retry and circuit breaker policy
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetHttpRetryAndCircuitBreakerPolicy(
        ILogger logger,
        string policyName = "HttpRetryAndCircuitBreaker")
    {
        var retryPolicy = GetHttpRetryPolicy(logger, policyName);
        var circuitBreakerPolicy = GetHttpCircuitBreakerPolicy(logger, policyName);

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Creates an HTTP retry policy with exponential backoff
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetHttpRetryPolicy(
        ILogger logger,
        string policyName = "HttpRetry",
        int maxRetryAttempts = 3)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // HttpRequestException and 5XX and 408 HTTP status codes
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: maxRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var exception = outcome.Exception;
                    var result = outcome.Result;

                    if (exception != null)
                    {
                        logger.LogWarning(
                            "HTTP retry {RetryCount}/{MaxRetries} for {PolicyName} due to exception: {Exception}. " +
                            "Waiting {Delay}ms before next attempt. Context: {@Context}",
                            retryCount, maxRetryAttempts, policyName, exception.Message, timespan.TotalMilliseconds, context);
                    }
                    else if (result != null)
                    {
                        logger.LogWarning(
                            "HTTP retry {RetryCount}/{MaxRetries} for {PolicyName} due to unsuccessful status: {StatusCode}. " +
                            "Waiting {Delay}ms before next attempt. Context: {@Context}",
                            retryCount, maxRetryAttempts, policyName, result.StatusCode, timespan.TotalMilliseconds, context);
                    }
                });
    }

    /// <summary>
    /// Creates an HTTP circuit breaker policy
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetHttpCircuitBreakerPolicy(
        ILogger logger,
        string policyName = "HttpCircuitBreaker",
        int handledEventsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        durationOfBreak ??= TimeSpan.FromSeconds(30);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: handledEventsAllowedBeforeBreaking,
                durationOfBreak: durationOfBreak.Value,
                onBreak: (delegateResult, timespan) =>
                {
                    var exception = delegateResult.Exception;
                    var result = delegateResult.Result;

                    if (exception != null)
                    {
                        logger.LogError(
                            "Circuit breaker {PolicyName} opened due to exception: {Exception}. " +
                            "Breaking for {BreakDuration}ms",
                            policyName, exception.Message, timespan.TotalMilliseconds);
                    }
                    else if (result != null)
                    {
                        logger.LogError(
                            "Circuit breaker {PolicyName} opened due to unsuccessful status: {StatusCode}. " +
                            "Breaking for {BreakDuration}ms",
                            policyName, result.StatusCode, timespan.TotalMilliseconds);
                    }
                },
                onReset: () =>
                {
                    logger.LogInformation(
                        "Circuit breaker {PolicyName} reset - accepting requests again",
                        policyName);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation(
                        "Circuit breaker {PolicyName} is half-open - testing with next request",
                        policyName);
                });
    }

    /// <summary>
    /// Creates a database retry policy
    /// </summary>
    public static IAsyncPolicy GetDatabaseRetryPolicy(
        ILogger logger,
        string policyName = "DatabaseRetry",
        int maxRetryAttempts = 3)
    {
        return Policy
            .Handle<InvalidOperationException>() // Common EF Core exceptions
            .Or<TimeoutException>()
            .Or<HttpRequestException>() // For database connection issues
            .WaitAndRetryAsync(
                retryCount: maxRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    logger.LogWarning(
                        "Database retry {RetryCount}/{MaxRetries} for {PolicyName} due to exception: {Exception}. " +
                        "Waiting {Delay}ms before next attempt. Context: {@Context}",
                        retryCount, maxRetryAttempts, policyName, exception.Message, timespan.TotalMilliseconds, context);
                });
    }

    /// <summary>
    /// Creates a database circuit breaker policy
    /// </summary>
    public static IAsyncPolicy GetDatabaseCircuitBreakerPolicy(
        ILogger logger,
        string policyName = "DatabaseCircuitBreaker",
        int handledEventsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        durationOfBreak ??= TimeSpan.FromSeconds(60);

        return Policy
            .Handle<InvalidOperationException>()
            .Or<TimeoutException>()
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: handledEventsAllowedBeforeBreaking,
                durationOfBreak: durationOfBreak.Value,
                onBreak: (exception, timespan) =>
                {
                    logger.LogError(
                        "Database circuit breaker {PolicyName} opened due to exception: {Exception}. " +
                        "Breaking for {BreakDuration}ms",
                        policyName, exception.Message, timespan.TotalMilliseconds);
                },
                onReset: () =>
                {
                    logger.LogInformation(
                        "Database circuit breaker {PolicyName} reset - accepting requests again",
                        policyName);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation(
                        "Database circuit breaker {PolicyName} is half-open - testing with next request",
                        policyName);
                });
    }

    /// <summary>
    /// Creates a Kafka retry policy
    /// </summary>
    public static IAsyncPolicy GetKafkaRetryPolicy(
        ILogger logger,
        string policyName = "KafkaRetry",
        int maxRetryAttempts = 3)
    {
        return Policy
            .Handle<Confluent.Kafka.KafkaException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                retryCount: maxRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    logger.LogWarning(
                        "Kafka retry {RetryCount}/{MaxRetries} for {PolicyName} due to exception: {Exception}. " +
                        "Waiting {Delay}ms before next attempt. Context: {@Context}",
                        retryCount, maxRetryAttempts, policyName, exception.Message, timespan.TotalMilliseconds, context);
                });
    }

    /// <summary>
    /// Creates a combined database retry and circuit breaker policy
    /// </summary>
    public static IAsyncPolicy GetDatabaseRetryAndCircuitBreakerPolicy(
        ILogger logger,
        string policyName = "DatabaseRetryAndCircuitBreaker")
    {
        var retryPolicy = GetDatabaseRetryPolicy(logger, policyName);
        var circuitBreakerPolicy = GetDatabaseCircuitBreakerPolicy(logger, policyName);

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Creates a combined Kafka retry and circuit breaker policy
    /// </summary>
    public static IAsyncPolicy GetKafkaRetryAndCircuitBreakerPolicy(
        ILogger logger,
        string policyName = "KafkaRetryAndCircuitBreaker")
    {
        var retryPolicy = GetKafkaRetryPolicy(logger, policyName);
        var circuitBreakerPolicy = GetKafkaCircuitBreakerPolicy(logger, policyName);

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Creates a Kafka circuit breaker policy
    /// </summary>
    public static IAsyncPolicy GetKafkaCircuitBreakerPolicy(
        ILogger logger,
        string policyName = "KafkaCircuitBreaker",
        int handledEventsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        durationOfBreak ??= TimeSpan.FromSeconds(30);

        return Policy
            .Handle<Confluent.Kafka.KafkaException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: handledEventsAllowedBeforeBreaking,
                durationOfBreak: durationOfBreak.Value,
                onBreak: (exception, timespan) =>
                {
                    logger.LogError(
                        "Kafka circuit breaker {PolicyName} opened due to exception: {Exception}. " +
                        "Breaking for {BreakDuration}ms",
                        policyName, exception.Message, timespan.TotalMilliseconds);
                },
                onReset: () =>
                {
                    logger.LogInformation(
                        "Kafka circuit breaker {PolicyName} reset - accepting requests again",
                        policyName);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation(
                        "Kafka circuit breaker {PolicyName} is half-open - testing with next request",
                        policyName);
                });
    }
}
