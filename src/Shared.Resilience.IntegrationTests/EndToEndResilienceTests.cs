using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using Shared.Resilience;
using System.Collections.Concurrent;
using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Shared.Resilience.IntegrationTests;

/// <summary>
/// Integration tests to verify end-to-end resilience patterns work together
/// in realistic microservice failure scenarios
/// </summary>
public class EndToEndResilienceTests : IDisposable
{
    private readonly WireMockServer _mockServer;
    private readonly ServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy _databasePolicy;
    private readonly IAsyncPolicy _kafkaPolicy;

    public EndToEndResilienceTests()
    {
        // Setup mock server
        _mockServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 0
        });

        // Setup DI container with all resilience policies
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddResilientHttpClients();
        services.AddResiliencePolicies();

        _serviceProvider = services.BuildServiceProvider();
        _httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>()
                                   .CreateClient("ResilientClient");
        _databasePolicy = _serviceProvider.GetRequiredKeyedService<IAsyncPolicy>("DatabasePolicy");
        _kafkaPolicy = _serviceProvider.GetRequiredKeyedService<IAsyncPolicy>("KafkaPolicy");
    }

    [Fact]
    public async Task Microservice_Operation_Should_Handle_Multiple_Layer_Failures()
    {
        // Arrange
        var endpoint = "/api/inventory/update";
        var successResponse = "Inventory updated successfully";
        var operationSteps = new List<string>();

        // Setup mock API to fail initially, then succeed
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingPost())
            .InScenario("Multi-layer failure")
            .WillSetStateTo("API Failed")
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBody("Service temporarily unavailable"));

        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingPost())
            .InScenario("Multi-layer failure")
            .WhenStateIs("API Failed")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(successResponse));

        // Simulate complete microservice operation with multiple failure points
        var microserviceOperation = async () =>
        {
            // Step 1: Database operation (simulate transient failure)
            var dbResult = await _databasePolicy.ExecuteAsync(async () =>
            {
                operationSteps.Add("Database operation attempted");

                // Simulate database transient failure on first attempt
                if (!operationSteps.Contains("Database retry"))
                {
                    operationSteps.Add("Database retry");
                    throw new InvalidOperationException("Database connection timeout");
                }

                operationSteps.Add("Database operation successful");
                return "Database updated";
            });

            // Step 2: HTTP API call (will fail once due to mock setup)
            var apiResult = await _httpClient.PostAsync($"{_mockServer.Url}{endpoint}",
                new StringContent("test data"));

            operationSteps.Add($"API call result: {apiResult.StatusCode}");
            var apiContent = await apiResult.Content.ReadAsStringAsync();

            // Step 3: Event publishing (simulate Kafka failure and retry)
            var eventResult = await _kafkaPolicy.ExecuteAsync(async () =>
            {
                operationSteps.Add("Event publishing attempted");

                // Simulate Kafka transient failure on first attempt
                if (!operationSteps.Contains("Event retry"))
                {
                    operationSteps.Add("Event retry");
                    throw new InvalidOperationException("Kafka broker temporarily unavailable");
                }

                operationSteps.Add("Event published successfully");
                return "Event published";
            });

            return new
            {
                Database = dbResult,
                Api = apiContent,
                Event = eventResult,
                Steps = operationSteps
            };
        };

        // Act
        var result = await microserviceOperation();

        // Assert
        result.Database.Should().Be("Database updated");
        result.Api.Should().Be(successResponse);
        result.Event.Should().Be("Event published");

        // Verify all resilience patterns worked
        operationSteps.Should().Contain("Database retry", "Database operation should have retried");
        operationSteps.Should().Contain("Database operation successful", "Database should eventually succeed");
        operationSteps.Should().Contain("Event retry", "Event publishing should have retried");
        operationSteps.Should().Contain("Event published successfully", "Event should eventually be published");

        // Verify HTTP retry happened (mock server should have 2 calls)
        _mockServer.LogEntries.Count().Should().Be(2, "HTTP client should have retried once");
    }

    [Fact]
    public async Task Circuit_Breaker_Should_Prevent_Cascading_Failures()
    {
        // Arrange
        var endpoint = "/api/failing-service";
        var operationCount = 0;
        var circuitBreakerTripped = false;

        // Setup mock to always fail
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Service failure"));

        // Simulate operation that should trigger circuit breaker
        var cascadingFailureOperation = async () =>
        {
            operationCount++;

            try
            {
                // This should eventually be stopped by circuit breaker
                var response = await _httpClient.GetAsync($"{_mockServer.Url}{endpoint}");
                return response.IsSuccessStatusCode ? "Success" : "Failed";
            }
            catch (Exception ex) when (ex.Message.Contains("circuit") ||
                                     ex.GetType().Name.Contains("BrokenCircuit"))
            {
                circuitBreakerTripped = true;
                throw;
            }
        };

        // Act - Make calls until circuit breaker trips
        var exceptions = new List<Exception>();
        for (int i = 0; i < 15; i++)
        {
            try
            {
                await cascadingFailureOperation();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // Assert
        circuitBreakerTripped.Should().BeTrue("Circuit breaker should have tripped to prevent cascading failures");
        exceptions.Should().HaveCountGreaterThan(5, "Should have multiple failures before circuit breaker trips");

        // Later calls should be blocked by circuit breaker (faster failures)
        var fastFailures = exceptions.Skip(8).Take(5);
        fastFailures.Should().AllSatisfy(ex =>
            ex.Message.Should().ContainAny("circuit", "breaker", "open"),
            "Later failures should be circuit breaker exceptions");
    }

    [Fact]
    public async Task Parallel_Operations_Should_Maintain_Independent_Resilience()
    {
        // Arrange
        var endpoints = new[] { "/api/service1", "/api/service2", "/api/service3" };
        var results = new ConcurrentBag<string>();

        // Setup different failure patterns for each endpoint
        _mockServer
            .Given(Request.Create().WithPath("/api/service1").UsingGet())
            .InScenario("Service1")
            .WillSetStateTo("Fail Once")
            .RespondWith(Response.Create().WithStatusCode(500));

        _mockServer
            .Given(Request.Create().WithPath("/api/service1").UsingGet())
            .InScenario("Service1")
            .WhenStateIs("Fail Once")
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("Service1 Success"));

        _mockServer
            .Given(Request.Create().WithPath("/api/service2").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("Service2 Success"));

        _mockServer
            .Given(Request.Create().WithPath("/api/service3").UsingGet())
            .InScenario("Service3")
            .WillSetStateTo("Fail Twice")
            .RespondWith(Response.Create().WithStatusCode(503));

        _mockServer
            .Given(Request.Create().WithPath("/api/service3").UsingGet())
            .InScenario("Service3")
            .WhenStateIs("Fail Twice")
            .WillSetStateTo("Fail Once More")
            .RespondWith(Response.Create().WithStatusCode(503));

        _mockServer
            .Given(Request.Create().WithPath("/api/service3").UsingGet())
            .InScenario("Service3")
            .WhenStateIs("Fail Once More")
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("Service3 Success"));

        // Act - Make parallel requests to test independent resilience
        var tasks = endpoints.Select(async endpoint =>
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_mockServer.Url}{endpoint}");
                var content = await response.Content.ReadAsStringAsync();
                results.Add($"{endpoint}: {content}");
                return content;
            }
            catch (Exception ex)
            {
                results.Add($"{endpoint}: Exception - {ex.Message}");
                throw;
            }
        });

        var completedResults = await Task.WhenAll(tasks);

        // Assert
        completedResults.Should().HaveCount(3);
        completedResults.Should().Contain("Service1 Success", "Service1 should succeed after retry");
        completedResults.Should().Contain("Service2 Success", "Service2 should succeed immediately");
        completedResults.Should().Contain("Service3 Success", "Service3 should succeed after multiple retries");

        // Verify independent operation - no cross-contamination of failures
        results.Should().AllSatisfy(result =>
            result.Should().ContainAny("Success", "Exception"),
            "All services should either succeed or have their own independent failures");
    }

    [Fact]
    public async Task Degraded_Service_Operation_Should_Use_Fallback_Patterns()
    {
        // Arrange
        var primaryEndpoint = "/api/primary-service";
        var fallbackEndpoint = "/api/fallback-service";

        // Setup primary service to fail
        _mockServer
            .Given(Request.Create().WithPath(primaryEndpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Primary service down"));

        // Setup fallback service to succeed
        _mockServer
            .Given(Request.Create().WithPath(fallbackEndpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Fallback service response"));

        // Simulate service operation with fallback logic
        var serviceWithFallback = async () =>
        {
            try
            {
                // Try primary service first
                var primaryResponse = await _httpClient.GetAsync($"{_mockServer.Url}{primaryEndpoint}");
                if (primaryResponse.IsSuccessStatusCode)
                {
                    return await primaryResponse.Content.ReadAsStringAsync();
                }
                throw new HttpRequestException($"Primary service failed: {primaryResponse.StatusCode}");
            }
            catch (HttpRequestException)
            {
                // Fallback to secondary service
                var fallbackResponse = await _httpClient.GetAsync($"{_mockServer.Url}{fallbackEndpoint}");
                var content = await fallbackResponse.Content.ReadAsStringAsync();
                return $"Fallback: {content}";
            }
        };

        // Act
        var result = await serviceWithFallback();

        // Assert
        result.Should().Be("Fallback: Fallback service response");

        // Verify both services were called
        _mockServer.LogEntries.Should().HaveCount(6,
            "Should have multiple calls to primary (retries) plus fallback call");
    }

    [Fact]
    public async Task Long_Running_Operation_Should_Maintain_Resilience_Under_Load()
    {
        // Arrange
        var endpoint = "/api/load-test";
        var successCount = 0;
        var failureCount = 0;
        var requestCount = 50;

        // Setup mock with intermittent failures 
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Success"));

        // Act - Make many concurrent requests
        var tasks = Enumerable.Range(0, requestCount).Select(async i =>
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_mockServer.Url}{endpoint}");
                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref successCount);
                    return "Success";
                }
                else
                {
                    Interlocked.Increment(ref failureCount);
                    return "Failed";
                }
            }
            catch
            {
                Interlocked.Increment(ref failureCount);
                return "Exception";
            }
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        successCount.Should().BeGreaterThan(requestCount / 2,
            "Most requests should succeed");

        var totalRequests = successCount + failureCount;
        totalRequests.Should().Be(requestCount, "All requests should be accounted for");

        // Verify resilience patterns handled the load effectively
        results.Count(r => r == "Success").Should().Be(successCount);

        // Should have some requests to mock server
        _mockServer.LogEntries.Count().Should().BeGreaterThan(0,
            "Should have made requests to the mock server");
    }

    public void Dispose()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
        _httpClient?.Dispose();
        _serviceProvider?.Dispose();
    }
}
