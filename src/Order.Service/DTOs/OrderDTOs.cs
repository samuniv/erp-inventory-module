using System.ComponentModel.DataAnnotations;

namespace Order.Service.DTOs;

/// <summary>
/// Request DTO for creating a new order
/// </summary>
public class CreateOrderRequest
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string CustomerEmail { get; set; } = string.Empty;

    [Phone]
    [MaxLength(15)]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> OrderItems { get; set; } = new();
}

/// <summary>
/// Request DTO for order items
/// </summary>
public class CreateOrderItemRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int InventoryItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ItemSku { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(50)]
    public string Unit { get; set; } = "pieces";

    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    public int? SupplierId { get; set; }

    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for created orders
/// </summary>
public class OrderResponse
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal SubtotalAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemResponse> OrderItems { get; set; } = new();
}

/// <summary>
/// Response DTO for order items
/// </summary>
public class OrderItemResponse
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemSku { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
}
