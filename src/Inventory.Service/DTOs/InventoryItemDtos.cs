using System.ComponentModel.DataAnnotations;

namespace Inventory.Service.DTOs;

public class CreateInventoryItemDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int StockLevel { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderThreshold { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(20)]
    public string Unit { get; set; } = "pieces";

    public int? SupplierId { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }
}

public class UpdateInventoryItemDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int StockLevel { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderThreshold { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(20)]
    public string Unit { get; set; } = "pieces";

    public int? SupplierId { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
}

public class InventoryItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int StockLevel { get; set; }
    public int ReorderThreshold { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = "pieces";
    public int? SupplierId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
}

public class InventoryItemListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int StockLevel { get; set; }
    public int ReorderThreshold { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = "pieces";
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock => StockLevel <= ReorderThreshold;
}
