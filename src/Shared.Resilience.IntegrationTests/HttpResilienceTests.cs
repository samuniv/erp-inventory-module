using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Resilience;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Shared.Resilience.IntegrationTests;

/// <summary>
/// Integration tests to verify HTTP resilience policies work correctly under failure scenarios
/// </summary>
public class HttpResilienceTests : IDisposable
{
    private readonly WireMockServer _mockServer;
    private readonly ServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;

    public HttpResilienceTests()
    {
        // Create a WireMock server to simulate external API failures
        _mockServer = WireMockServer.Start(new WireMockServerSettings 
        { 
            Port = 0 // Use dynamic port
        });

        // Setup DI container with resilience policies
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddResilientHttpClients();
        
        _serviceProvider = services.BuildServiceProvider();
        _httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>()
                                   .CreateClient("ResilientClient");
    }

    [Fact]
    public async Task Http_Retry_Policy_Should_Retry_On_Transient_Failures()
    {
        // Arrange
        var endpoint = "/test-retry";
        var expectedSuccessResponse = "Success after retry";
        
        // Setup mock to fail twice then succeed
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .InScenario("Retry Test")
            .WillSetStateTo("First Call")
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Internal Server Error"));

        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .InScenario("Retry Test")
            .WhenStateIs("First Call")
            .WillSetStateTo("Second Call")
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBody("Service Unavailable"));

        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .InScenario("Retry Test")
            .WhenStateIs("Second Call")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(expectedSuccessResponse));

        var requestUri = $"{_mockServer.Url}{endpoint}";

        // Act & Assert - Should succeed after retries
        var response = await _httpClient.GetAsync(requestUri);
        var content = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Be(expectedSuccessResponse);
        
        // Verify all three calls were made (original + 2 retries)
        _mockServer.LogEntries.Count().Should().Be(3);
    }

    [Fact]
    public async Task Http_Circuit_Breaker_Should_Open_After_Consecutive_Failures()
    {
        // Arrange
        var endpoint = "/test-circuit-breaker";
        
        // Setup mock to always fail
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Persistent Failure"));

        var requestUri = $"{_mockServer.Url}{endpoint}";

        // Act - Make enough calls to trip the circuit breaker (5 failures)
        var exceptions = new List<Exception>();
        
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await _httpClient.GetAsync(requestUri);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // Assert
        exceptions.Should().HaveCountGreaterThan(5, "Circuit breaker should prevent further calls after threshold");
        
        // Some exceptions should be circuit breaker exceptions (not just HTTP)
        exceptions.Should().Contain(ex => ex.Message.Contains("circuit") || 
                                        ex.GetType().Name.Contains("BrokenCircuit"));
    }

    [Fact]
    public async Task Http_Retry_Policy_Should_Use_Exponential_Backoff()
    {
        // Arrange
        var endpoint = "/test-backoff";
        
        // Setup mock to fail multiple times
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Server Error"));

        var requestUri = $"{_mockServer.Url}{endpoint}";
        var startTime = DateTime.UtcNow;

        // Act
        try
        {
            await _httpClient.GetAsync(requestUri);
        }
        catch
        {
            // Expected to fail after retries
        }

        var totalTime = DateTime.UtcNow - startTime;

        // Assert - Should take some time due to exponential backoff
        // With exponential backoff: ~2s + ~4s + ~8s = at least 14 seconds total
        totalTime.Should().BeGreaterThan(TimeSpan.FromSeconds(10), 
            "Exponential backoff should introduce delays between retries");
        
        // Verify the expected number of retry attempts were made
        _mockServer.LogEntries.Count().Should().Be(4, "Should make 1 initial call + 3 retries");
    }

    [Fact]
    public async Task Http_Policy_Should_Not_Retry_On_Client_Errors()
    {
        // Arrange
        var endpoint = "/test-no-retry";
        
        // Setup mock to return 400 Bad Request (client error - should not retry)
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBody("Bad Request"));

        var requestUri = $"{_mockServer.Url}{endpoint}";

        // Act
        var response = await _httpClient.GetAsync(requestUri);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        
        // Should only make one call (no retries for client errors)
        _mockServer.LogEntries.Count().Should().Be(1, "Client errors should not be retried");
    }

    [Fact]
    public async Task Http_Circuit_Breaker_Should_Recovery_After_Timeout()
    {
        // Arrange
        var endpoint = "/test-recovery";
        
        // Setup mock to fail initially, then succeed
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .InScenario("Recovery Test")
            .WillSetStateTo("Failing")
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Initial Failure"));

        var requestUri = $"{_mockServer.Url}{endpoint}";

        // Act - Trip the circuit breaker
        var exceptions = new List<Exception>();
        for (int i = 0; i < 6; i++)
        {
            try
            {
                await _httpClient.GetAsync(requestUri);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // Wait for circuit breaker to potentially move to half-open
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Setup mock to succeed after recovery period
        _mockServer.Reset();
        _mockServer
            .Given(Request.Create().WithPath(endpoint).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Recovery Success"));

        // Act - Try again after circuit breaker timeout
        await Task.Delay(TimeSpan.FromSeconds(30)); // Wait for circuit breaker timeout
        
        var recoveryResponse = await _httpClient.GetAsync(requestUri);

        // Assert
        recoveryResponse.IsSuccessStatusCode.Should().BeTrue("Circuit breaker should allow requests after timeout");
        
        var content = await recoveryResponse.Content.ReadAsStringAsync();
        content.Should().Be("Recovery Success");
    }

    public void Dispose()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
        _httpClient?.Dispose();
        _serviceProvider?.Dispose();
    }
}
