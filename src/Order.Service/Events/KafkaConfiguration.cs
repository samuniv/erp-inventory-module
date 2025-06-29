namespace Order.Service.Events;

/// <summary>
/// Configuration for Kafka topics and event handling
/// </summary>
public static class KafkaTopics
{
    // Topics this service publishes to
    public const string OrderCreated = "order.created";
    public const string OrderCompleted = "order.completed";
    public const string OrderCancelled = "order.cancelled";

    // Topics this service consumes from
    public const string InventoryAlerts = "inventory.alerts";
    public const string InventoryUpdated = "inventory.updated";
}

/// <summary>
/// Kafka configuration options
/// </summary>
public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "order-service";
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public bool EnableIdempotence { get; set; } = true;
    public string Acks { get; set; } = "all";
    public int MessageTimeoutMs { get; set; } = 30000;
    public int MaxRetries { get; set; } = 5;
    public int RetryBackoffMs { get; set; } = 100;
    public int RequestTimeoutMs { get; set; } = 30000;
    public int DeliveryTimeoutMs { get; set; } = 120000;

    // Consumer specific settings
    public string AutoOffsetReset { get; set; } = "earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int SessionTimeoutMs { get; set; } = 6000;
    public int HeartbeatIntervalMs { get; set; } = 3000;
    public int MaxPollIntervalMs { get; set; } = 300000;
}

/// <summary>
/// Event metadata for tracking and correlation
/// </summary>
public record EventMetadata
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;
    public string EventVersion { get; init; } = "1.0";
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string Source { get; init; } = "Order.Service";
    public string? UserId { get; init; }
    public Dictionary<string, string>? Properties { get; init; }
}
