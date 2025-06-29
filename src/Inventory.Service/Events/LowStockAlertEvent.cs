using System.Text.Json.Serialization;

namespace Inventory.Service.Events;

/// <summary>
/// Event published when an inventory item's stock level falls below the low stock threshold
/// </summary>
public class LowStockAlertEvent
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
    /// Current stock level of the item
    /// </summary>
    [JsonPropertyName("currentStockLevel")]
    public int CurrentStockLevel { get; set; }

    /// <summary>
    /// The low stock threshold that was breached
    /// </summary>
    [JsonPropertyName("lowStockThreshold")]
    public int LowStockThreshold { get; set; }

    /// <summary>
    /// Category of the inventory item
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Supplier ID associated with the item
    /// </summary>
    [JsonPropertyName("supplierId")]
    public int? SupplierId { get; set; }

    /// <summary>
    /// UTC timestamp when the alert was generated
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Severity level of the alert (Info, Warning, Critical)
    /// </summary>
    [JsonPropertyName("severity")]
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Unique identifier for tracking this specific alert
    /// </summary>
    [JsonPropertyName("alertId")]
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Severity levels for low stock alerts
/// </summary>
public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
