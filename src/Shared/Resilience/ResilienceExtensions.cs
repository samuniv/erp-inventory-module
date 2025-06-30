using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Shared.Resilience;

/// <summary>
/// Extension methods for configuring resilience patterns in services
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Adds HTTP clients to the service collection
    /// </summary>
    public static IServiceCollection AddResilientHttpClients(
        this IServiceCollection services,
        Action<ResilientHttpClientOptions>? configureOptions = null)
    {
        var options = new ResilientHttpClientOptions();
        configureOptions?.Invoke(options);

        // Add default HTTP client
        services.AddHttpClient("Default", client =>
        {
            client.Timeout = options.HttpTimeout;
        });

        // Add resilient HTTP client with retry and circuit breaker policies
        services.AddHttpClient("ResilientClient", client =>
        {
            client.Timeout = options.HttpTimeout;
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<object>>();
            return PollyPolicies.GetHttpRetryAndCircuitBreakerPolicy(logger, "ResilientClient");
        });

        // Add named HTTP clients for specific services if needed
        if (options.ConfigureInventoryClient)
        {
            services.AddHttpClient("InventoryClient", client =>
            {
                client.BaseAddress = new Uri(options.InventoryServiceBaseUrl);
                client.Timeout = options.HttpTimeout;
            });
        }

        if (options.ConfigureOrderClient)
        {
            services.AddHttpClient("OrderClient", client =>
            {
                client.BaseAddress = new Uri(options.OrderServiceBaseUrl);
                client.Timeout = options.HttpTimeout;
            });
        }

        if (options.ConfigureSupplierClient)
        {
            services.AddHttpClient("SupplierClient", client =>
            {
                client.BaseAddress = new Uri(options.SupplierServiceBaseUrl);
                client.Timeout = options.HttpTimeout;
            });
        }

        if (options.ConfigureAuthClient)
        {
            services.AddHttpClient("AuthClient", client =>
            {
                client.BaseAddress = new Uri(options.AuthServiceBaseUrl);
                client.Timeout = options.HttpTimeout;
            });
        }

        return services;
    }

    /// <summary>
    /// Adds resilience policies as singletons to the service collection
    /// </summary>
    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services)
    {
        // Register database policies with keyed service
        services.AddKeyedSingleton<IAsyncPolicy>("DatabasePolicy", (serviceProvider, key) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<object>>();
            return PollyPolicies.GetDatabaseRetryAndCircuitBreakerPolicy(logger, "DatabasePolicy");
        });

        // Register Kafka policies with keyed service
        services.AddKeyedSingleton<IAsyncPolicy>("KafkaPolicy", (serviceProvider, key) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<object>>();
            return PollyPolicies.GetKafkaRetryAndCircuitBreakerPolicy(logger, "KafkaPolicy");
        });

        return services;
    }
}

/// <summary>
/// Configuration options for resilient HTTP clients
/// </summary>
public class ResilientHttpClientOptions
{
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool ConfigureInventoryClient { get; set; } = false;
    public string InventoryServiceBaseUrl { get; set; } = "http://localhost:5007";

    public bool ConfigureOrderClient { get; set; } = false;
    public string OrderServiceBaseUrl { get; set; } = "http://localhost:5008";

    public bool ConfigureSupplierClient { get; set; } = false;
    public string SupplierServiceBaseUrl { get; set; } = "http://localhost:5009";

    public bool ConfigureAuthClient { get; set; } = false;
    public string AuthServiceBaseUrl { get; set; } = "http://localhost:5006";
}
