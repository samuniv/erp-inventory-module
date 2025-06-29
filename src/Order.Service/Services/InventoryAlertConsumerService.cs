using Confluent.Kafka;
using Order.Service.Events;
using System.Text.Json;

namespace Order.Service.Services;

/// <summary>
/// Background service for consuming inventory alert events from Kafka
/// </summary>
public class InventoryAlertConsumerService : BackgroundService
{
    private readonly ILogger<InventoryAlertConsumerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaOptions _kafkaOptions;
    private IConsumer<string, string>? _consumer;

    public InventoryAlertConsumerService(
        ILogger<InventoryAlertConsumerService> logger,
        IServiceProvider serviceProvider,
        Microsoft.Extensions.Options.IOptions<KafkaOptions> kafkaOptions)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Inventory Alert Consumer Service");

        try
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _kafkaOptions.BootstrapServers,
                GroupId = _kafkaOptions.GroupId,
                AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_kafkaOptions.AutoOffsetReset, true),
                EnableAutoCommit = _kafkaOptions.EnableAutoCommit,
                SessionTimeoutMs = _kafkaOptions.SessionTimeoutMs,
                HeartbeatIntervalMs = _kafkaOptions.HeartbeatIntervalMs,
                MaxPollIntervalMs = _kafkaOptions.MaxPollIntervalMs,
                ClientId = "order-service-consumer"
            };

            using (_consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
                .SetStatisticsHandler((_, json) => _logger.LogDebug("Kafka consumer statistics: {Statistics}", json))
                .SetPartitionsAssignedHandler((c, partitions) =>
                {
                    _logger.LogInformation("Assigned partitions: [{Partitions}]", 
                        string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
                })
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    _logger.LogInformation("Revoked partitions: [{Partitions}]", 
                        string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
                })
                .Build())
            {
                _consumer.Subscribe(new[] { KafkaTopics.InventoryAlerts, KafkaTopics.InventoryUpdated });
                _logger.LogInformation("Subscribed to topics: {Topics}", 
                    string.Join(", ", new[] { KafkaTopics.InventoryAlerts, KafkaTopics.InventoryUpdated }));

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(stoppingToken);
                        
                        if (consumeResult?.Message != null)
                        {
                            await ProcessMessageAsync(consumeResult, stoppingToken);
                            
                            // Commit the offset after successful processing
                            _consumer.Commit(consumeResult);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
                        
                        // Wait before retrying
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error processing Kafka message");
                        
                        // Wait before retrying
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error in Inventory Alert Consumer Service");
            throw;
        }
        finally
        {
            _logger.LogInformation("Inventory Alert Consumer Service stopped");
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
    {
        var message = consumeResult.Message;
        var topic = consumeResult.Topic;
        var partition = consumeResult.Partition;
        var offset = consumeResult.Offset;

        _logger.LogDebug("Processing message from topic {Topic}, partition {Partition}, offset {Offset}", 
            topic, partition, offset);

        try
        {
            switch (topic)
            {
                case KafkaTopics.InventoryAlerts:
                    await ProcessInventoryAlertAsync(message.Value, cancellationToken);
                    break;

                case KafkaTopics.InventoryUpdated:
                    await ProcessInventoryUpdatedAsync(message.Value, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Received message from unknown topic: {Topic}", topic);
                    break;
            }

            _logger.LogDebug("Successfully processed message from topic {Topic}, partition {Partition}, offset {Offset}", 
                topic, partition, offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message from topic {Topic}, partition {Partition}, offset {Offset}. Message: {Message}", 
                topic, partition, offset, message.Value);
            throw;
        }
    }

    private async Task ProcessInventoryAlertAsync(string messageValue, CancellationToken cancellationToken)
    {
        try
        {
            var inventoryAlert = JsonSerializer.Deserialize<InventoryAlertEvent>(messageValue, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (inventoryAlert == null)
            {
                _logger.LogWarning("Failed to deserialize inventory alert message: {Message}", messageValue);
                return;
            }

            _logger.LogInformation("Processing inventory alert for item {ItemSku}: {Message}", 
                inventoryAlert.Sku, inventoryAlert.Message);

            // Use a scoped service to handle the alert
            using var scope = _serviceProvider.CreateScope();
            var alertHandler = scope.ServiceProvider.GetRequiredService<IInventoryAlertHandler>();
            
            var result = await alertHandler.HandleInventoryAlertAsync(inventoryAlert, cancellationToken);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully processed inventory alert for item {ItemSku}", inventoryAlert.Sku);
            }
            else
            {
                _logger.LogWarning("Failed to process inventory alert for item {ItemSku}: {Error}", 
                    inventoryAlert.Sku, result.ErrorMessage);
                    
                if (result.ShouldRetry)
                {
                    throw new InvalidOperationException($"Retryable error processing inventory alert: {result.ErrorMessage}");
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize inventory alert message: {Message}", messageValue);
            // Don't retry for deserialization errors
        }
    }

    private async Task ProcessInventoryUpdatedAsync(string messageValue, CancellationToken cancellationToken)
    {
        try
        {
            // For now, just log the inventory update
            // This could be extended to update local cache or trigger other business logic
            _logger.LogInformation("Received inventory update: {Message}", messageValue);
            
            // Add any specific inventory update processing logic here
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process inventory update: {Message}", messageValue);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Inventory Alert Consumer Service");
        
        try
        {
            _consumer?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing Kafka consumer");
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        try
        {
            _consumer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Kafka consumer");
        }
        
        base.Dispose();
    }
}

/// <summary>
/// Interface for handling inventory alerts
/// </summary>
public interface IInventoryAlertHandler
{
    Task<EventProcessingResult> HandleInventoryAlertAsync(InventoryAlertEvent alertEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of inventory alert handler
/// </summary>
public class InventoryAlertHandler : IInventoryAlertHandler
{
    private readonly ILogger<InventoryAlertHandler> _logger;

    public InventoryAlertHandler(ILogger<InventoryAlertHandler> logger)
    {
        _logger = logger;
    }

    public async Task<EventProcessingResult> HandleInventoryAlertAsync(InventoryAlertEvent alertEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Handling inventory alert for item {ItemSku}: {AlertType} - {Message}", 
                alertEvent.Sku, alertEvent.AlertType, alertEvent.Message);

            // Business logic for handling inventory alerts
            switch (alertEvent.AlertType?.ToLowerInvariant())
            {
                case "lowstock":
                    await HandleLowStockAlert(alertEvent, cancellationToken);
                    break;

                case "outofstock":
                    await HandleOutOfStockAlert(alertEvent, cancellationToken);
                    break;

                case "restock":
                    await HandleRestockAlert(alertEvent, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown alert type: {AlertType} for item {ItemSku}", 
                        alertEvent.AlertType, alertEvent.Sku);
                    break;
            }

            return EventProcessingResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling inventory alert for item {ItemSku}", alertEvent.Sku);
            return EventProcessingResult.Failure(ex.Message, shouldRetry: true);
        }
    }

    private async Task HandleLowStockAlert(InventoryAlertEvent alertEvent, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Low stock alert for item {ItemSku}: Current quantity {CurrentQuantity}, Threshold {Threshold}", 
            alertEvent.Sku, alertEvent.CurrentQuantity, alertEvent.MinimumThreshold);

        // Implement business logic for low stock alerts
        // Examples:
        // - Notify procurement team
        // - Automatically create purchase orders
        // - Update order fulfillment priority
        // - Send notifications to relevant stakeholders

        await Task.CompletedTask;
    }

    private async Task HandleOutOfStockAlert(InventoryAlertEvent alertEvent, CancellationToken cancellationToken)
    {
        _logger.LogError("Out of stock alert for item {ItemSku}: Current quantity {CurrentQuantity}", 
            alertEvent.Sku, alertEvent.CurrentQuantity);

        // Implement business logic for out of stock alerts
        // Examples:
        // - Pause new orders for this item
        // - Update item availability status
        // - Trigger emergency procurement
        // - Notify customers with pending orders

        await Task.CompletedTask;
    }

    private async Task HandleRestockAlert(InventoryAlertEvent alertEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Restock alert for item {ItemSku}: Current quantity {CurrentQuantity}", 
            alertEvent.Sku, alertEvent.CurrentQuantity);

        // Implement business logic for restock alerts
        // Examples:
        // - Resume accepting orders for this item
        // - Update item availability status
        // - Process any backorders
        // - Notify customers waiting for the item

        await Task.CompletedTask;
    }
}
