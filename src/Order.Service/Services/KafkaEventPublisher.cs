using Confluent.Kafka;
using Order.Service.Events;
using System.Text.Json;

namespace Order.Service.Services;

/// <summary>
/// Service for publishing events to Kafka
/// </summary>
public interface IEventPublisher
{
    Task<EventPublishResult> PublishAsync<T>(string topic, T eventData, string? partitionKey = null, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// Kafka-based event publisher implementation
/// </summary>
public class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed = false;

    public KafkaEventPublisher(IConfiguration configuration, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;

        var kafkaOptions = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            EnableIdempotence = kafkaOptions.EnableIdempotence,
            Acks = Enum.Parse<Acks>(kafkaOptions.Acks, true),
            MessageTimeoutMs = kafkaOptions.MessageTimeoutMs,
            RetryBackoffMs = kafkaOptions.RetryBackoffMs,
            RequestTimeoutMs = kafkaOptions.RequestTimeoutMs,
            CompressionType = CompressionType.Snappy,
            BatchSize = 16384,
            LingerMs = 10,
            ClientId = "order-service-producer"
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error: {Reason}", e.Reason))
            .SetStatisticsHandler((_, json) => _logger.LogDebug("Kafka producer statistics: {Statistics}", json))
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _logger.LogInformation("Kafka event publisher initialized with bootstrap servers: {BootstrapServers}", kafkaOptions.BootstrapServers);
    }

    public async Task<EventPublishResult> PublishAsync<T>(string topic, T eventData, string? partitionKey = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(eventData, _jsonOptions);
            var key = partitionKey ?? Guid.NewGuid().ToString();

            var message = new Message<string, string>
            {
                Key = key,
                Value = json,
                Headers = new Headers
                {
                    { "eventType", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) },
                    { "contentType", System.Text.Encoding.UTF8.GetBytes("application/json") },
                    { "source", System.Text.Encoding.UTF8.GetBytes("Order.Service") },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()) }
                }
            };

            _logger.LogDebug("Publishing event to topic {Topic} with key {Key}", topic, key);

            var deliveryResult = await _producer.ProduceAsync(topic, message, cancellationToken);

            _logger.LogInformation("Successfully published event {EventType} to topic {Topic}, partition {Partition}, offset {Offset}",
                typeof(T).Name, topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);

            return EventPublishResult.Success(topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to topic {Topic}: {Error}",
                typeof(T).Name, topic, ex.Error.Reason);
            return EventPublishResult.Failure($"Kafka producer error: {ex.Error.Reason}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing event {EventType} to topic {Topic}",
                typeof(T).Name, topic);
            return EventPublishResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _producer?.Flush(TimeSpan.FromSeconds(10));
                _producer?.Dispose();
                _logger.LogInformation("Kafka event publisher disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Kafka event publisher");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
