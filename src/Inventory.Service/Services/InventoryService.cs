using Inventory.Service.Data;
using Inventory.Service.DTOs;
using Inventory.Service.Events;
using Inventory.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service.Services;

public interface IInventoryService
{
    Task<IEnumerable<InventoryItemListDto>> GetAllItemsAsync();
    Task<InventoryItemDto?> GetItemByIdAsync(int id);
    Task<InventoryItemDto?> GetItemBySkuAsync(string sku);
    Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemDto createDto);
    Task<InventoryItemDto?> UpdateItemAsync(int id, UpdateInventoryItemDto updateDto);
    Task<bool> DeleteItemAsync(int id);
    Task<IEnumerable<InventoryItemListDto>> GetLowStockItemsAsync();
    Task<bool> AdjustStockAsync(int id, int adjustment, string reason = "Manual adjustment");
}

public class InventoryService : IInventoryService
{
    private readonly InventoryDbContext _context;
    private readonly IAlertProducerService _alertProducer;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        InventoryDbContext context,
        IAlertProducerService alertProducer,
        ILogger<InventoryService> logger)
    {
        _context = context;
        _alertProducer = alertProducer;
        _logger = logger;
    }

    public async Task<IEnumerable<InventoryItemListDto>> GetAllItemsAsync()
    {
        var items = await _context.InventoryItems
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync();

        return items.Select(MapToListDto);
    }

    public async Task<InventoryItemDto?> GetItemByIdAsync(int id)
    {
        var item = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);

        return item != null ? MapToDto(item) : null;
    }

    public async Task<InventoryItemDto?> GetItemBySkuAsync(string sku)
    {
        var item = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.Sku == sku && i.IsActive);

        return item != null ? MapToDto(item) : null;
    }

    public async Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemDto createDto)
    {
        // Check if SKU already exists
        var existingItem = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.Sku == createDto.Sku);

        if (existingItem != null)
        {
            throw new InvalidOperationException($"An item with SKU '{createDto.Sku}' already exists.");
        }

        var item = new InventoryItem
        {
            Name = createDto.Name,
            Description = createDto.Description,
            Sku = createDto.Sku,
            StockLevel = createDto.StockLevel,
            ReorderThreshold = createDto.ReorderThreshold,
            UnitPrice = createDto.UnitPrice,
            Unit = createDto.Unit,
            SupplierId = createDto.SupplierId,
            Category = createDto.Category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new inventory item: {ItemName} (SKU: {Sku})", item.Name, item.Sku);

        // Check if the new item is already low stock
        if (item.StockLevel <= item.ReorderThreshold)
        {
            await PublishLowStockAlert(item);
        }

        // Publish inventory updated event
        await _alertProducer.PublishInventoryUpdatedAsync(new InventoryUpdatedEvent
        {
            ItemId = item.Id,
            ItemName = item.Name,
            Sku = item.Sku,
            CurrentStockLevel = item.StockLevel,
            PreviousStockLevel = null,
            OperationType = InventoryOperation.Created,
            Category = item.Category,
            Price = item.UnitPrice,
            SupplierId = item.SupplierId,
            UpdatedBy = "system"
        });

        return MapToDto(item);
    }

    public async Task<InventoryItemDto?> UpdateItemAsync(int id, UpdateInventoryItemDto updateDto)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null || !item.IsActive)
        {
            return null;
        }

        var previousStockLevel = item.StockLevel;
        var stockChange = updateDto.StockLevel - previousStockLevel;

        // Update item properties
        item.Name = updateDto.Name;
        item.Description = updateDto.Description;
        item.StockLevel = updateDto.StockLevel;
        item.ReorderThreshold = updateDto.ReorderThreshold;
        item.UnitPrice = updateDto.UnitPrice;
        item.Unit = updateDto.Unit;
        item.SupplierId = updateDto.SupplierId;
        item.Category = updateDto.Category;
        item.IsActive = updateDto.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated inventory item: {ItemName} (ID: {ItemId}), Stock: {PreviousStock} -> {NewStock}",
            item.Name, item.Id, previousStockLevel, item.StockLevel);

        // Check for low stock alert
        if (item.StockLevel <= item.ReorderThreshold)
        {
            await PublishLowStockAlert(item);
        }

        // Publish inventory updated event if stock changed
        if (stockChange != 0)
        {
            await _alertProducer.PublishInventoryUpdatedAsync(new InventoryUpdatedEvent
            {
                ItemId = item.Id,
                ItemName = item.Name,
                Sku = item.Sku,
                CurrentStockLevel = item.StockLevel,
                PreviousStockLevel = previousStockLevel,
                OperationType = InventoryOperation.Updated,
                Category = item.Category,
                Price = item.UnitPrice,
                SupplierId = item.SupplierId,
                UpdatedBy = "system"
            });
        }

        return MapToDto(item);
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null)
        {
            return false;
        }

        // Soft delete
        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Soft deleted inventory item: {ItemName} (ID: {ItemId})", item.Name, item.Id);

        return true;
    }

    public async Task<IEnumerable<InventoryItemListDto>> GetLowStockItemsAsync()
    {
        var items = await _context.InventoryItems
            .Where(i => i.IsActive && i.StockLevel <= i.ReorderThreshold)
            .OrderBy(i => i.StockLevel)
            .ThenBy(i => i.Name)
            .ToListAsync();

        return items.Select(MapToListDto);
    }

    public async Task<bool> AdjustStockAsync(int id, int adjustment, string reason = "Manual adjustment")
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null || !item.IsActive)
        {
            return false;
        }

        var previousStockLevel = item.StockLevel;
        item.StockLevel += adjustment;

        // Ensure stock doesn't go negative
        if (item.StockLevel < 0)
        {
            item.StockLevel = 0;
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Stock adjusted for {ItemName} (ID: {ItemId}): {PreviousStock} -> {NewStock} (adjustment: {Adjustment})",
            item.Name, item.Id, previousStockLevel, item.StockLevel, adjustment);

        // Check for low stock alert
        if (item.StockLevel <= item.ReorderThreshold)
        {
            await PublishLowStockAlert(item);
        }

        // Publish inventory updated event
        await _alertProducer.PublishInventoryUpdatedAsync(new InventoryUpdatedEvent
        {
            ItemId = item.Id,
            ItemName = item.Name,
            Sku = item.Sku,
            CurrentStockLevel = item.StockLevel,
            PreviousStockLevel = previousStockLevel,
            OperationType = InventoryOperation.StockAdjusted,
            Category = item.Category,
            Price = item.UnitPrice,
            SupplierId = item.SupplierId,
            UpdatedBy = "system",
            Metadata = new Dictionary<string, object> { { "reason", reason }, { "adjustment", adjustment } }
        });

        return true;
    }

    private async Task PublishLowStockAlert(InventoryItem item)
    {
        var severity = item.StockLevel == 0 ? AlertSeverity.Critical :
                      item.StockLevel <= (item.ReorderThreshold * 0.5) ? AlertSeverity.Warning :
                      AlertSeverity.Info;

        var alertEvent = new LowStockAlertEvent
        {
            ItemId = item.Id,
            ItemName = item.Name,
            Sku = item.Sku,
            CurrentStockLevel = item.StockLevel,
            LowStockThreshold = item.ReorderThreshold,
            Category = item.Category,
            SupplierId = item.SupplierId,
            Severity = severity
        };

        await _alertProducer.PublishLowStockAlertAsync(alertEvent);

        _logger.LogWarning(
            "Low stock alert published for {ItemName} (ID: {ItemId}): {CurrentStock} <= {Threshold}",
            item.Name, item.Id, item.StockLevel, item.ReorderThreshold);
    }

    private static InventoryItemDto MapToDto(InventoryItem item)
    {
        return new InventoryItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Sku = item.Sku,
            StockLevel = item.StockLevel,
            ReorderThreshold = item.ReorderThreshold,
            UnitPrice = item.UnitPrice,
            Unit = item.Unit,
            SupplierId = item.SupplierId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            Category = item.Category,
            IsActive = item.IsActive
        };
    }

    private static InventoryItemListDto MapToListDto(InventoryItem item)
    {
        return new InventoryItemListDto
        {
            Id = item.Id,
            Name = item.Name,
            Sku = item.Sku,
            StockLevel = item.StockLevel,
            ReorderThreshold = item.ReorderThreshold,
            UnitPrice = item.UnitPrice,
            Unit = item.Unit,
            Category = item.Category,
            IsActive = item.IsActive
        };
    }
}
