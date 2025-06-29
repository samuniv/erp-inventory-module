using Confluent.Kafka;
using Inventory.Service.Events;
using System.Text.Json;

namespace Inventory.Service.Services;

public interface IAlertProducerService
{
    Task PublishLowStockAlertAsync(LowStockAlertEvent alertEvent);
    Task PublishInventoryUpdatedAsync(InventoryUpdatedEvent updatedEvent);
}

public class AlertProducerService : IAlertProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<AlertProducerService> _logger;
    private readonly string _lowStockTopic;
    private readonly string _inventoryUpdatedTopic;

    public AlertProducerService(IConfiguration configuration, ILogger<AlertProducerService> logger)
    {
        _logger = logger;
        
        var config = new ProducerConfig
        {
            BootstrapServers = configuration.GetConnectionString("Kafka") ?? "localhost:9092",
            ClientId = "inventory-service-producer",
            // Ensure message delivery
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,
            // Enable idempotence to prevent duplicate messages
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error: {Error}", e.Reason))
            .Build();

        _lowStockTopic = configuration["Kafka:Topics:LowStockAlerts"] ?? "inventory.alerts";
        _inventoryUpdatedTopic = configuration["Kafka:Topics:InventoryUpdated"] ?? "inventory.updated";
    }

    public async Task PublishLowStockAlertAsync(LowStockAlertEvent alertEvent)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = $"item-{alertEvent.ItemId}",
                Value = JsonSerializer.Serialize(alertEvent),
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes("LowStockAlert") },
                    { "source", System.Text.Encoding.UTF8.GetBytes("inventory-service") },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(alertEvent.Timestamp.ToString("O")) }
                }
            };

            var result = await _producer.ProduceAsync(_lowStockTopic, message);
            
            _logger.LogInformation(
                "Low stock alert published for item {ItemId} ({ItemName}) to topic {Topic} at offset {Offset}",
                alertEvent.ItemId, alertEvent.ItemName, _lowStockTopic, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, 
                "Failed to publish low stock alert for item {ItemId}: {Error}", 
                alertEvent.ItemId, ex.Error.Reason);
            throw;
        }
    }

    public async Task PublishInventoryUpdatedAsync(InventoryUpdatedEvent updatedEvent)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = $"item-{updatedEvent.ItemId}",
                Value = JsonSerializer.Serialize(updatedEvent),
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes("InventoryUpdated") },
                    { "source", System.Text.Encoding.UTF8.GetBytes("inventory-service") },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(updatedEvent.Timestamp.ToString("O")) }
                }
            };

            var result = await _producer.ProduceAsync(_inventoryUpdatedTopic, message);
            
            _logger.LogInformation(
                "Inventory updated event published for item {ItemId} ({ItemName}) to topic {Topic} at offset {Offset}",
                updatedEvent.ItemId, updatedEvent.ItemName, _inventoryUpdatedTopic, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, 
                "Failed to publish inventory updated event for item {ItemId}: {Error}", 
                updatedEvent.ItemId, ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}
