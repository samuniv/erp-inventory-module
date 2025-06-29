using System.Text.Json.Serialization;

namespace Inventory.Service.Events;

/// <summary>
/// Event published when an inventory item is updated (created, modified, or stock changed)
/// </summary>
public class InventoryUpdatedEvent
{
    /// <summary>
    /// Unique identifier of the inventory item
    /// </summary>
    [JsonPropertyName("itemId")]
    public int ItemId { get; set; }

    /// <summary>
    /// Name of the inventory item
    /// </summary>
    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// SKU of the inventory item
    /// </summary>
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Current stock level after the update
    /// </summary>
    [JsonPropertyName("currentStockLevel")]
    public int CurrentStockLevel { get; set; }

    /// <summary>
    /// Previous stock level before the update (null for new items)
    /// </summary>
    [JsonPropertyName("previousStockLevel")]
    public int? PreviousStockLevel { get; set; }

    /// <summary>
    /// Type of operation that triggered this event
    /// </summary>
    [JsonPropertyName("operationType")]
    public InventoryOperation OperationType { get; set; }

    /// <summary>
    /// Category of the inventory item
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Price of the inventory item
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Supplier ID associated with the item
    /// </summary>
    [JsonPropertyName("supplierId")]
    public int? SupplierId { get; set; }

    /// <summary>
    /// UTC timestamp when the update occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User or system that performed the update
    /// </summary>
    [JsonPropertyName("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata or notes about the update
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Types of inventory operations that can trigger an update event
/// </summary>
public enum InventoryOperation
{
    Created = 0,
    Updated = 1,
    StockAdjusted = 2,
    Deleted = 3,
    StockReceived = 4,
    StockAllocated = 5
}
