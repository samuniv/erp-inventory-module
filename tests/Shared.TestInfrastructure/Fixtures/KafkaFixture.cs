using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Testcontainers.Kafka;
using Xunit;

namespace Shared.TestInfrastructure.Fixtures;

/// <summary>
/// Base fixture for Kafka message broker integration tests using Testcontainers
/// </summary>
public class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _container;
    private readonly ILogger<KafkaFixture> _logger;

    public string BootstrapServers => _container.GetBootstrapAddress();

    public KafkaFixture()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<KafkaFixture>();

        _container = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.1")
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "true")
            .WithPortBinding(0, 9092) // Random port to avoid conflicts
            .Build();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Starting Kafka container...");
            await _container.StartAsync();
            
            // Kafka needs time to initialize
            _logger.LogInformation("Waiting for Kafka to fully initialize...");
            await Task.Delay(TimeSpan.FromSeconds(15));
            
            _logger.LogInformation("Kafka container started successfully. Bootstrap servers: {BootstrapServers}", BootstrapServers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Kafka container");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _logger.LogInformation("Stopping Kafka container...");
            await _container.DisposeAsync();
            _logger.LogInformation("Kafka container stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing Kafka container");
        }
    }

    /// <summary>
    /// Create a consumer for testing Kafka messages
    /// </summary>
    public IConsumer<string, string> CreateKafkaConsumer(string groupId)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 10000,
            HeartbeatIntervalMs = 3000
        };

        return new ConsumerBuilder<string, string>(config).Build();
    }

    /// <summary>
    /// Create a producer for testing Kafka messages
    /// </summary>
    public IProducer<string, string> CreateKafkaProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,
            RequestTimeoutMs = 30000
        };

        return new ProducerBuilder<string, string>(config).Build();
    }

    /// <summary>
    /// Create a topic for testing
    /// </summary>
    public async Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1)
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = BootstrapServers
        }).Build();

        try
        {
            // Note: Topic creation through admin client not implemented in this version
            // Topics will be auto-created when first message is published
            _logger.LogInformation("Topic auto-creation enabled, topic will be created on first message: {TopicName}", topicName);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Topic creation for {TopicName} result: {Error}", topicName, ex.Message);
        }
    }

    /// <summary>
    /// Wait for a message on a topic (useful for testing message production)
    /// </summary>
    public async Task<ConsumeResult<string, string>?> WaitForMessageAsync(
        string topic, 
        string consumerGroup, 
        TimeSpan timeout,
        Func<ConsumeResult<string, string>, bool>? predicate = null)
    {
        using var consumer = CreateKafkaConsumer(consumerGroup);
        consumer.Subscribe(topic);

        var endTime = DateTime.UtcNow.Add(timeout);
        
        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result != null)
                {
                    if (predicate == null || predicate(result))
                    {
                        return result;
                    }
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Error consuming message from topic {Topic}", topic);
            }
        }

        return null;
    }
}
