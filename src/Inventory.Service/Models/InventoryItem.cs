using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory.Service.Models;

[Table("INVENTORY_ITEMS")]
public class InventoryItem
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("DESCRIPTION")]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("SKU")]
    public string Sku { get; set; } = string.Empty;

    [Column("STOCK_LEVEL")]
    public int StockLevel { get; set; }

    [Column("REORDER_THRESHOLD")]
    public int ReorderThreshold { get; set; }

    [Column("UNIT_PRICE", TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    [MaxLength(20)]
    [Column("UNIT")]
    public string Unit { get; set; } = "pieces";

    [Column("SUPPLIER_ID")]
    public int? SupplierId { get; set; }

    [Column("CREATED_AT", TypeName = "timestamp")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UPDATED_AT", TypeName = "timestamp")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    [Column("CATEGORY")]
    public string? Category { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;
}
