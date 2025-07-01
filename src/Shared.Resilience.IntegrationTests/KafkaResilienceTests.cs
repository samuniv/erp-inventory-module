using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using Shared.Resilience;
using Testcontainers.Kafka;
using Confluent.Kafka;

namespace Shared.Resilience.IntegrationTests;

/// <summary>
/// Integration tests to verify Kafka resilience policies work correctly under failure scenarios
/// </summary>
public class KafkaResilienceTests : IAsyncLifetime
{
    private readonly KafkaContainer _kafkaContainer;
    private ServiceProvider _serviceProvider;
    private IAsyncPolicy _kafkaPolicy;

    public KafkaResilienceTests()
    {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.4.0")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();

        // Setup DI container with resilience policies
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddResiliencePolicies();

        _serviceProvider = services.BuildServiceProvider();
        _kafkaPolicy = _serviceProvider.GetRequiredKeyedService<IAsyncPolicy>("KafkaPolicy");
    }

    [Fact]
    public async Task Kafka_Retry_Policy_Should_Retry_On_Transient_Producer_Errors()
    {
        // Arrange
        var bootstrapServers = _kafkaContainer.GetBootstrapAddress();
        var retryCount = 0;
        var topic = "test-retry-topic";

        // Create topic first
        await CreateTopicAsync(bootstrapServers, topic);

        // Simulate Kafka producer operation that fails transiently
        var kafkaOperation = async () =>
        {
            retryCount++;

            // Simulate transient failure for first two attempts
            if (retryCount <= 2)
            {
                throw new ProduceException<string, string>(
                    new Error(ErrorCode.RequestTimedOut, "Request timed out", false),
                    new DeliveryResult<string, string>());
            }

            // Succeed on third attempt
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                ClientId = "test-producer"
            };

            using var producer = new ProducerBuilder<string, string>(config).Build();

            var result = await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = "test-key",
                Value = $"Test message - attempt {retryCount}"
            });

            return result.Status.ToString();
        };

        // Act
        var result = await _kafkaPolicy.ExecuteAsync(async () => await kafkaOperation());

        // Assert
        result.Should().Be("Persisted");
        retryCount.Should().Be(3, "Should retry twice before succeeding on third attempt");
    }

    [Fact]
    public async Task Kafka_Retry_Policy_Should_Not_Retry_On_Non_Transient_Errors()
    {
        // Arrange
        var retryCount = 0;

        // Simulate Kafka operation that fails with non-transient error
        var kafkaOperation = async () =>
        {
            retryCount++;

            // Simulate non-transient failure (authentication error)
            throw new ProduceException<string, string>(
                new Error(ErrorCode.SaslAuthenticationFailed, "Authentication failed", false),
                new DeliveryResult<string, string>());
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProduceException<string, string>>(
            async () => await _kafkaPolicy.ExecuteAsync(async () => await kafkaOperation()));

        exception.Error.Code.Should().Be(ErrorCode.SaslAuthenticationFailed);
        retryCount.Should().Be(1, "Should not retry non-transient errors");
    }

    [Fact]
    public async Task Kafka_Policy_Should_Handle_Broker_Unavailable_Scenarios()
    {
        // Arrange
        var bootstrapServers = _kafkaContainer.GetBootstrapAddress();
        var brokerUnavailableCount = 0;
        var topic = "test-broker-unavailable";

        // Create topic first
        await CreateTopicAsync(bootstrapServers, topic);

        // Simulate broker unavailable scenario that resolves after retry
        var kafkaOperation = async () =>
        {
            brokerUnavailableCount++;

            if (brokerUnavailableCount == 1)
            {
                // Simulate broker unavailable on first attempt
                throw new ProduceException<string, string>(
                    new Error(ErrorCode.BrokerNotAvailable, "Broker not available", false),
                    new DeliveryResult<string, string>());
            }

            // Succeed on retry
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                ClientId = "test-producer-broker"
            };

            using var producer = new ProducerBuilder<string, string>(config).Build();

            var result = await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = "broker-test",
                Value = "Broker recovery test message"
            });

            return result.Status.ToString();
        };

        // Act
        var result = await _kafkaPolicy.ExecuteAsync(async () => await kafkaOperation());

        // Assert
        result.Should().Be("Persisted");
        brokerUnavailableCount.Should().Be(2, "Should retry once after broker unavailable");
    }

    [Fact]
    public async Task Kafka_Policy_Should_Handle_Message_Size_Limit_Scenarios()
    {
        // Arrange
        var bootstrapServers = _kafkaContainer.GetBootstrapAddress();
        var messageSizeAttempts = 0;
        var topic = "test-message-size";

        // Create topic first
        await CreateTopicAsync(bootstrapServers, topic);

        // Simulate message size adjustment scenario
        var kafkaOperation = async () =>
        {
            messageSizeAttempts++;

            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                ClientId = "test-producer-size"
            };

            using var producer = new ProducerBuilder<string, string>(config).Build();

            // Create message that might be too large initially
            var messageSize = messageSizeAttempts == 1 ? 1000000 : 1000; // 1MB vs 1KB
            var largeMessage = new string('A', messageSize);

            if (messageSizeAttempts == 1)
            {
                // Simulate message too large error
                throw new ProduceException<string, string>(
                    new Error(ErrorCode.MsgSizeTooLarge, "Message size too large", false),
                    new DeliveryResult<string, string>());
            }

            var result = await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = "size-test",
                Value = largeMessage
            });

            return result.Status.ToString();
        };

        // Act
        var result = await _kafkaPolicy.ExecuteAsync(async () => await kafkaOperation());

        // Assert
        result.Should().Be("Persisted");
        messageSizeAttempts.Should().Be(2, "Should retry once with smaller message");
    }

    [Fact]
    public async Task Kafka_Policy_Should_Respect_Maximum_Retry_Attempts()
    {
        // Arrange
        var retryCount = 0;
        const int maxExpectedRetries = 3; // Based on our policy configuration

        // Simulate persistent Kafka failure
        var failingOperation = async () =>
        {
            retryCount++;
            throw new ProduceException<string, string>(
                new Error(ErrorCode.RequestTimedOut, "Persistent timeout", false),
                new DeliveryResult<string, string>());
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProduceException<string, string>>(
            async () => await _kafkaPolicy.ExecuteAsync(async () => await failingOperation()));

        exception.Error.Code.Should().Be(ErrorCode.RequestTimedOut);
        retryCount.Should().BeLessThanOrEqualTo(maxExpectedRetries + 1);
    }

    [Fact]
    public async Task Kafka_Policy_Should_Use_Exponential_Backoff()
    {
        // Arrange
        var retryCount = 0;
        var retryTimestamps = new List<DateTime>();

        // Simulate operation that tracks retry timing
        var timedOperation = async () =>
        {
            retryTimestamps.Add(DateTime.UtcNow);
            retryCount++;

            if (retryCount <= 2)
            {
                throw new ProduceException<string, string>(
                    new Error(ErrorCode.RequestTimedOut, "Timeout for timing test", false),
                    new DeliveryResult<string, string>());
            }

            return "Timing test completed";
        };

        // Act
        var result = await _kafkaPolicy.ExecuteAsync(async () => await timedOperation());

        // Assert
        result.Should().Be("Timing test completed");
        retryTimestamps.Should().HaveCount(3, "Should have timestamps for all attempts");

        if (retryTimestamps.Count >= 2)
        {
            var firstRetryDelay = retryTimestamps[1] - retryTimestamps[0];
            firstRetryDelay.Should().BeGreaterThan(TimeSpan.FromMilliseconds(500),
                "First retry should have exponential backoff delay");
        }

        if (retryTimestamps.Count >= 3)
        {
            var secondRetryDelay = retryTimestamps[2] - retryTimestamps[1];
            secondRetryDelay.Should().BeGreaterThan(TimeSpan.FromSeconds(1),
                "Second retry should have longer exponential backoff delay");
        }
    }

    [Fact]
    public async Task Kafka_Consumer_Should_Handle_Connection_Loss()
    {
        // Arrange
        var bootstrapServers = _kafkaContainer.GetBootstrapAddress();
        var topic = "test-consumer-resilience";
        var connectionLossCount = 0;

        // Create topic and produce a test message
        await CreateTopicAsync(bootstrapServers, topic);
        await ProduceTestMessageAsync(bootstrapServers, topic, "test-consumer-message");

        // Simulate consumer operation with connection loss
        var consumerOperation = async () =>
        {
            connectionLossCount++;

            if (connectionLossCount == 1)
            {
                // Simulate connection loss
                throw new ConsumeException(
                    new ConsumeResult<byte[], byte[]>(),
                    new Error(ErrorCode.NetworkException, "Network connection lost", false));
            }

            // Succeed on retry
            var config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = "test-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            using var consumer = new ConsumerBuilder<string, string>(config).Build();
            consumer.Subscribe(topic);

            var result = consumer.Consume(TimeSpan.FromSeconds(10));
            return result?.Message?.Value ?? "No message";
        };

        // Act
        var result = await _kafkaPolicy.ExecuteAsync(async () => await consumerOperation());

        // Assert
        result.Should().Be("test-consumer-message");
        connectionLossCount.Should().Be(2, "Should retry once after connection loss");
    }

    private async Task CreateTopicAsync(string bootstrapServers, string topicName)
    {
        // For now, we'll rely on auto-topic creation in Kafka
        // In a real scenario, you would create topics explicitly
        await Task.CompletedTask;
    }

    private async Task ProduceTestMessageAsync(string bootstrapServers, string topic, string message)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = "test-setup-producer"
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = "setup-key",
            Value = message
        });
    }

    public async Task DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync();
        _serviceProvider?.Dispose();
    }
}
