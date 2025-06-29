namespace Order.Service.Events;

/// <summary>
/// Base interface for all domain events
/// </summary>
public interface IEvent
{
    string EventId { get; }
    DateTime EventTimestamp { get; }
    string EventVersion { get; }
    string? CorrelationId { get; }
}

/// <summary>
/// Base interface for events that can be published to Kafka
/// </summary>
public interface IKafkaEvent : IEvent
{
    string GetTopicName();
    string GetPartitionKey();
}

/// <summary>
/// Event publishing result
/// </summary>
public record EventPublishResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Topic { get; init; }
    public int? Partition { get; init; }
    public long? Offset { get; init; }
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
    
    public static EventPublishResult Success(string topic, int partition, long offset)
        => new() { IsSuccess = true, Topic = topic, Partition = partition, Offset = offset };
    
    public static EventPublishResult Failure(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Event processing result for consumers
/// </summary>
public record EventProcessingResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public bool ShouldRetry { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
    
    public static EventProcessingResult Success()
        => new() { IsSuccess = true };
    
    public static EventProcessingResult Failure(string errorMessage, bool shouldRetry = true)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, ShouldRetry = shouldRetry };
}
