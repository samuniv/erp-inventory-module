using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Inventory.Service.Observability;

/// <summary>
/// Custom metrics for Inventory Service
/// </summary>
public class InventoryMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _itemsCreatedCounter;
    private readonly Counter<long> _itemsUpdatedCounter;
    private readonly Counter<long> _itemsDeletedCounter;
    private readonly Counter<long> _stockAlertsTriggeredCounter;
    private readonly Counter<long> _stockReplenishmentsCounter;
    private readonly Histogram<double> _stockLevelsHistogram;
    private readonly Histogram<double> _reorderThresholdHistogram;
    private readonly UpDownCounter<long> _lowStockItemsGauge;
    private readonly UpDownCounter<long> _totalActiveItemsGauge;

    public InventoryMetrics()
    {
        _meter = new Meter("Inventory.Service", "1.0.0");

        // Counters for inventory operations
        _itemsCreatedCounter = _meter.CreateCounter<long>(
            "inventory_items_created_total",
            description: "Total number of inventory items created");

        _itemsUpdatedCounter = _meter.CreateCounter<long>(
            "inventory_items_updated_total",
            description: "Total number of inventory items updated");

        _itemsDeletedCounter = _meter.CreateCounter<long>(
            "inventory_items_deleted_total",
            description: "Total number of inventory items deleted");

        _stockAlertsTriggeredCounter = _meter.CreateCounter<long>(
            "stock_alerts_triggered_total",
            description: "Total number of low stock alerts triggered");

        _stockReplenishmentsCounter = _meter.CreateCounter<long>(
            "stock_replenishments_total",
            description: "Total number of stock replenishment operations");

        // Histograms for distribution metrics
        _stockLevelsHistogram = _meter.CreateHistogram<double>(
            "stock_levels_distribution",
            unit: "units",
            description: "Distribution of inventory stock levels");

        _reorderThresholdHistogram = _meter.CreateHistogram<double>(
            "reorder_threshold_distribution",
            unit: "units",
            description: "Distribution of reorder threshold values");

        // Gauges for current state
        _lowStockItemsGauge = _meter.CreateUpDownCounter<long>(
            "low_stock_items_current",
            description: "Current number of items below reorder threshold");

        _totalActiveItemsGauge = _meter.CreateUpDownCounter<long>(
            "active_inventory_items_current",
            description: "Current total number of active inventory items");
    }

    /// <summary>
    /// Records a new inventory item creation
    /// </summary>
    /// <param name="itemId">Item ID</param>
    /// <param name="category">Item category</param>
    /// <param name="stockLevel">Initial stock level</param>
    /// <param name="reorderThreshold">Reorder threshold</param>
    public void RecordItemCreated(Guid itemId, string category, int stockLevel, int reorderThreshold)
    {
        var tags = new TagList
        {
            {"item_id", itemId.ToString()},
            {"category", category},
            {"stock_range", GetStockRange(stockLevel)},
            {"is_low_stock", (stockLevel <= reorderThreshold).ToString().ToLower()}
        };

        _itemsCreatedCounter.Add(1, tags);
        _stockLevelsHistogram.Record(stockLevel, tags);
        _reorderThresholdHistogram.Record(reorderThreshold, tags);
        _totalActiveItemsGauge.Add(1);

        if (stockLevel <= reorderThreshold)
        {
            _lowStockItemsGauge.Add(1);
        }
    }

    /// <summary>
    /// Records inventory item update
    /// </summary>
    /// <param name="itemId">Item ID</param>
    /// <param name="category">Item category</param>
    /// <param name="oldStockLevel">Previous stock level</param>
    /// <param name="newStockLevel">New stock level</param>
    /// <param name="reorderThreshold">Reorder threshold</param>
    /// <param name="updateType">Type of update (manual, automatic, etc.)</param>
    public void RecordItemUpdated(Guid itemId, string category, int oldStockLevel, int newStockLevel, int reorderThreshold, string updateType)
    {
        var tags = new TagList
        {
            {"item_id", itemId.ToString()},
            {"category", category},
            {"update_type", updateType},
            {"stock_range", GetStockRange(newStockLevel)},
            {"stock_change", GetStockChangeType(oldStockLevel, newStockLevel)}
        };

        _itemsUpdatedCounter.Add(1, tags);
        _stockLevelsHistogram.Record(newStockLevel, tags);

        // Update low stock gauge
        var wasLowStock = oldStockLevel <= reorderThreshold;
        var isLowStock = newStockLevel <= reorderThreshold;

        if (!wasLowStock && isLowStock)
        {
            _lowStockItemsGauge.Add(1);
        }
        else if (wasLowStock && !isLowStock)
        {
            _lowStockItemsGauge.Add(-1);
        }
    }

    /// <summary>
    /// Records inventory item deletion
    /// </summary>
    /// <param name="itemId">Item ID</param>
    /// <param name="category">Item category</param>
    /// <param name="finalStockLevel">Final stock level before deletion</param>
    /// <param name="reorderThreshold">Reorder threshold</param>
    public void RecordItemDeleted(Guid itemId, string category, int finalStockLevel, int reorderThreshold)
    {
        var tags = new TagList
        {
            {"item_id", itemId.ToString()},
            {"category", category},
            {"final_stock_range", GetStockRange(finalStockLevel)}
        };

        _itemsDeletedCounter.Add(1, tags);
        _totalActiveItemsGauge.Add(-1);

        if (finalStockLevel <= reorderThreshold)
        {
            _lowStockItemsGauge.Add(-1);
        }
    }

    /// <summary>
    /// Records a stock alert being triggered
    /// </summary>
    /// <param name="itemId">Item ID</param>
    /// <param name="category">Item category</param>
    /// <param name="currentStock">Current stock level</param>
    /// <param name="reorderThreshold">Reorder threshold</param>
    /// <param name="alertType">Type of alert (low_stock, out_of_stock, etc.)</param>
    public void RecordStockAlertTriggered(Guid itemId, string category, int currentStock, int reorderThreshold, string alertType)
    {
        var tags = new TagList
        {
            {"item_id", itemId.ToString()},
            {"category", category},
            {"alert_type", alertType},
            {"stock_level", currentStock},
            {"reorder_threshold", reorderThreshold},
            {"urgency", GetAlertUrgency(currentStock, reorderThreshold)}
        };

        _stockAlertsTriggeredCounter.Add(1, tags);
    }

    /// <summary>
    /// Records stock replenishment
    /// </summary>
    /// <param name="itemId">Item ID</param>
    /// <param name="category">Item category</param>
    /// <param name="replenishmentQuantity">Quantity added</param>
    /// <param name="newStockLevel">New total stock level</param>
    /// <param name="replenishmentType">Type of replenishment (purchase, return, transfer, etc.)</param>
    public void RecordStockReplenishment(Guid itemId, string category, int replenishmentQuantity, int newStockLevel, string replenishmentType)
    {
        var tags = new TagList
        {
            {"item_id", itemId.ToString()},
            {"category", category},
            {"replenishment_type", replenishmentType},
            {"quantity_range", GetQuantityRange(replenishmentQuantity)},
            {"new_stock_range", GetStockRange(newStockLevel)}
        };

        _stockReplenishmentsCounter.Add(1, tags);
    }

    /// <summary>
    /// Records inventory search operation metrics
    /// </summary>
    /// <param name="searchType">Type of search (by_name, by_category, by_supplier, etc.)</param>
    /// <param name="resultCount">Number of results returned</param>
    /// <param name="executionTime">Time taken to execute search</param>
    public void RecordInventorySearch(string searchType, int resultCount, TimeSpan executionTime)
    {
        var searchCounter = _meter.CreateCounter<long>(
            "inventory_searches_total",
            description: "Total number of inventory search operations");

        var searchDurationHistogram = _meter.CreateHistogram<double>(
            "inventory_search_duration_seconds",
            unit: "s",
            description: "Duration of inventory search operations");

        var tags = new TagList
        {
            {"search_type", searchType},
            {"result_count_range", GetResultCountRange(resultCount)}
        };

        searchCounter.Add(1, tags);
        searchDurationHistogram.Record(executionTime.TotalSeconds, tags);
    }

    /// <summary>
    /// Categorizes stock levels into ranges
    /// </summary>
    private static string GetStockRange(int stockLevel)
    {
        return stockLevel switch
        {
            0 => "out_of_stock",
            <= 10 => "very_low",
            <= 50 => "low",
            <= 100 => "medium",
            <= 500 => "high",
            _ => "very_high"
        };
    }

    /// <summary>
    /// Determines stock change type
    /// </summary>
    private static string GetStockChangeType(int oldLevel, int newLevel)
    {
        return (newLevel - oldLevel) switch
        {
            > 0 => "increased",
            < 0 => "decreased",
            _ => "unchanged"
        };
    }

    /// <summary>
    /// Determines alert urgency level
    /// </summary>
    private static string GetAlertUrgency(int currentStock, int reorderThreshold)
    {
        if (currentStock == 0)
            return "critical";

        var percentage = (double)currentStock / reorderThreshold;
        return percentage switch
        {
            <= 0.25 => "high",
            <= 0.5 => "medium",
            _ => "low"
        };
    }

    /// <summary>
    /// Categorizes quantity ranges for replenishments
    /// </summary>
    private static string GetQuantityRange(int quantity)
    {
        return quantity switch
        {
            <= 10 => "small",
            <= 50 => "medium",
            <= 100 => "large",
            _ => "bulk"
        };
    }

    /// <summary>
    /// Categorizes search result count ranges
    /// </summary>
    private static string GetResultCountRange(int count)
    {
        return count switch
        {
            0 => "no_results",
            <= 10 => "few",
            <= 50 => "moderate",
            <= 100 => "many",
            _ => "extensive"
        };
    }

    public void Dispose()
    {
        _meter?.Dispose();
        GC.SuppressFinalize(this);
    }
}
