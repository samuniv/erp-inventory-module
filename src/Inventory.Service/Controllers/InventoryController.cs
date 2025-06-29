using Inventory.Service.DTOs;
using Inventory.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get all inventory items
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItemListDto>>> GetAllItems()
    {
        try
        {
            var items = await _inventoryService.GetAllItemsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all inventory items");
            return StatusCode(500, "An error occurred while retrieving inventory items");
        }
    }

    /// <summary>
    /// Get inventory item by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> GetItemById(int id)
    {
        try
        {
            var item = await _inventoryService.GetItemByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory item with ID {ItemId}", id);
            return StatusCode(500, "An error occurred while retrieving the inventory item");
        }
    }

    /// <summary>
    /// Get inventory item by SKU
    /// </summary>
    [HttpGet("sku/{sku}")]
    public async Task<ActionResult<InventoryItemDto>> GetItemBySku(string sku)
    {
        try
        {
            var item = await _inventoryService.GetItemBySkuAsync(sku);
            if (item == null)
            {
                return NotFound($"Inventory item with SKU '{sku}' not found");
            }
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory item with SKU {Sku}", sku);
            return StatusCode(500, "An error occurred while retrieving the inventory item");
        }
    }

    /// <summary>
    /// Create a new inventory item
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InventoryItemDto>> CreateItem([FromBody] CreateInventoryItemDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var item = await _inventoryService.CreateItemAsync(createDto);
            return CreatedAtAction(nameof(GetItemById), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating inventory item");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inventory item");
            return StatusCode(500, "An error occurred while creating the inventory item");
        }
    }

    /// <summary>
    /// Update an existing inventory item
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> UpdateItem(int id, [FromBody] UpdateInventoryItemDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var item = await _inventoryService.UpdateItemAsync(id, updateDto);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory item with ID {ItemId}", id);
            return StatusCode(500, "An error occurred while updating the inventory item");
        }
    }

    /// <summary>
    /// Delete an inventory item (soft delete)
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        try
        {
            var success = await _inventoryService.DeleteItemAsync(id);
            if (!success)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inventory item with ID {ItemId}", id);
            return StatusCode(500, "An error occurred while deleting the inventory item");
        }
    }

    /// <summary>
    /// Get all items with low stock levels
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<IEnumerable<InventoryItemListDto>>> GetLowStockItems()
    {
        try
        {
            var items = await _inventoryService.GetLowStockItemsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving low stock items");
            return StatusCode(500, "An error occurred while retrieving low stock items");
        }
    }

    /// <summary>
    /// Adjust stock level for an inventory item
    /// </summary>
    [HttpPost("{id:int}/adjust-stock")]
    public async Task<IActionResult> AdjustStock(int id, [FromBody] StockAdjustmentDto adjustmentDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _inventoryService.AdjustStockAsync(id, adjustmentDto.Adjustment, adjustmentDto.Reason);
            if (!success)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            return Ok(new { message = "Stock adjusted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting stock for inventory item with ID {ItemId}", id);
            return StatusCode(500, "An error occurred while adjusting stock");
        }
    }
}

public class StockAdjustmentDto
{
    public int Adjustment { get; set; }
    public string Reason { get; set; } = "Manual adjustment";
}
