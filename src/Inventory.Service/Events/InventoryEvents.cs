namespace Inventory.Service.Events;

public class LowStockAlertEvent
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int CurrentStockLevel { get; set; }
    public int ReorderThreshold { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string AlertLevel { get; set; } = "Warning"; // Warning, Critical
    public int? SupplierId { get; set; }
    public string? Category { get; set; }
}

public class InventoryUpdatedEvent
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int PreviousStockLevel { get; set; }
    public int NewStockLevel { get; set; }
    public int StockChange { get; set; }
    public string ChangeType { get; set; } = string.Empty; // "Increase", "Decrease", "Adjustment"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}
