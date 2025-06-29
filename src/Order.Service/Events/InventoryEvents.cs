using System.Text.Json.Serialization;

namespace Order.Service.Events;

/// <summary>
/// Event consumed from Inventory Service when stock levels change
/// </summary>
public record InventoryAlertEvent
{
    [JsonPropertyName("inventoryItemId")]
    public required string InventoryItemId { get; init; }

    [JsonPropertyName("sku")]
    public required string Sku { get; init; }

    [JsonPropertyName("productName")]
    public required string ProductName { get; init; }

    [JsonPropertyName("currentQuantity")]
    public int CurrentQuantity { get; init; }

    [JsonPropertyName("minimumThreshold")]
    public int MinimumThreshold { get; init; }

    [JsonPropertyName("alertType")]
    public string AlertType { get; init; } = "LowStock";

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "Medium";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("warehouseLocation")]
    public string? WarehouseLocation { get; init; }

    [JsonPropertyName("supplierId")]
    public string? SupplierId { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    [JsonPropertyName("eventTimestamp")]
    public DateTime EventTimestamp { get; init; }

    [JsonPropertyName("eventVersion")]
    public string EventVersion { get; init; } = "1.0";

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Event published when an order is completed
/// </summary>
public record OrderCompletedEvent
{
    [JsonPropertyName("orderId")]
    public required string OrderId { get; init; }

    [JsonPropertyName("orderNumber")]
    public required string OrderNumber { get; init; }

    [JsonPropertyName("customerId")]
    public required string CustomerId { get; init; }

    [JsonPropertyName("completedDate")]
    public DateTime CompletedDate { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("orderItems")]
    public required List<OrderItemDto> OrderItems { get; init; }

    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("eventTimestamp")]
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("eventVersion")]
    public string EventVersion { get; init; } = "1.0";

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Event published when an order is cancelled
/// </summary>
public record OrderCancelledEvent
{
    [JsonPropertyName("orderId")]
    public required string OrderId { get; init; }

    [JsonPropertyName("orderNumber")]
    public required string OrderNumber { get; init; }

    [JsonPropertyName("customerId")]
    public required string CustomerId { get; init; }

    [JsonPropertyName("cancelledDate")]
    public DateTime CancelledDate { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("orderItems")]
    public required List<OrderItemDto> OrderItems { get; init; }

    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("eventTimestamp")]
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("eventVersion")]
    public string EventVersion { get; init; } = "1.0";

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }
}
