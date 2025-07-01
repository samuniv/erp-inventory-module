using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Shared.TestInfrastructure.Utilities;

/// <summary>
/// Utilities for Kafka testing scenarios
/// </summary>
public static class KafkaTestUtilities
{
    /// <summary>
    /// Wait for a specific message on a topic with JSON deserialization
    /// </summary>
    public static async Task<T?> WaitForMessageAsync<T>(
        IConsumer<string, string> consumer,
        string topic,
        TimeSpan timeout,
        Func<T, bool>? predicate = null,
        ILogger? logger = null) where T : class
    {
        consumer.Subscribe(topic);
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result != null)
                {
                    logger?.LogInformation("Received message from topic {Topic}: {Message}", topic, result.Message.Value);

                    var message = JsonSerializer.Deserialize<T>(result.Message.Value);
                    if (message != null && (predicate == null || predicate(message)))
                    {
                        return message;
                    }
                }
            }
            catch (ConsumeException ex)
            {
                logger?.LogWarning(ex, "Error consuming message from topic {Topic}", topic);
            }
            catch (JsonException ex)
            {
                logger?.LogWarning(ex, "Error deserializing message from topic {Topic}", topic);
            }
        }

        return null;
    }

    /// <summary>
    /// Publish a message to a topic with JSON serialization
    /// </summary>
    public static async Task<DeliveryResult<string, string>> PublishMessageAsync<T>(
        IProducer<string, string> producer,
        string topic,
        T message,
        string? key = null,
        ILogger? logger = null) where T : class
    {
        var json = JsonSerializer.Serialize(message);

        logger?.LogInformation("Publishing message to topic {Topic}: {Message}", topic, json);

        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = json,
            Timestamp = new Timestamp(DateTime.UtcNow)
        };

        return await producer.ProduceAsync(topic, kafkaMessage);
    }

    /// <summary>
    /// Create a unique consumer group ID for testing
    /// </summary>
    public static string CreateTestConsumerGroup(string testName)
    {
        return $"test-{testName}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Create a unique topic name for testing
    /// </summary>
    public static string CreateTestTopic(string baseName)
    {
        return $"test-{baseName}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Wait for multiple messages on a topic
    /// </summary>
    public static async Task<List<T>> WaitForMessagesAsync<T>(
        IConsumer<string, string> consumer,
        string topic,
        int expectedCount,
        TimeSpan timeout,
        Func<T, bool>? predicate = null,
        ILogger? logger = null) where T : class
    {
        var messages = new List<T>();
        consumer.Subscribe(topic);
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime && messages.Count < expectedCount)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result != null)
                {
                    logger?.LogInformation("Received message {Count}/{Expected} from topic {Topic}: {Message}",
                        messages.Count + 1, expectedCount, topic, result.Message.Value);

                    var message = JsonSerializer.Deserialize<T>(result.Message.Value);
                    if (message != null && (predicate == null || predicate(message)))
                    {
                        messages.Add(message);
                    }
                }
            }
            catch (ConsumeException ex)
            {
                logger?.LogWarning(ex, "Error consuming message from topic {Topic}", topic);
            }
            catch (JsonException ex)
            {
                logger?.LogWarning(ex, "Error deserializing message from topic {Topic}", topic);
            }
        }

        return messages;
    }

    /// <summary>
    /// Consume raw string messages from a topic
    /// </summary>
    public static async Task<List<string>> ConsumeMessagesAsync(
        string topic,
        string bootstrapServers,
        TimeSpan timeout,
        int maxMessages = 10,
        ILogger? logger = null)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = CreateTestConsumerGroup("message-consumer"),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        var messages = new List<string>();
        consumer.Subscribe(topic);
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime && messages.Count < maxMessages)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result != null)
                {
                    logger?.LogInformation("Received raw message {Count}/{Max} from topic {Topic}: {Message}",
                        messages.Count + 1, maxMessages, topic, result.Message.Value);

                    messages.Add(result.Message.Value);
                }
            }
            catch (ConsumeException ex)
            {
                logger?.LogWarning(ex, "Error consuming message from topic {Topic}", topic);
            }
        }

        return messages;
    }
}
