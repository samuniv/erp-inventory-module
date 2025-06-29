using System.Text.Json.Serialization;

namespace Order.Service.Events;

/// <summary>
/// Event published when a new order is created
/// </summary>
public record OrderCreatedEvent
{
    [JsonPropertyName("orderId")]
    public required string OrderId { get; init; }

    [JsonPropertyName("orderNumber")]
    public required string OrderNumber { get; init; }

    [JsonPropertyName("customerId")]
    public required string CustomerId { get; init; }

    [JsonPropertyName("customerName")]
    public required string CustomerName { get; init; }

    [JsonPropertyName("customerEmail")]
    public required string CustomerEmail { get; init; }

    [JsonPropertyName("orderDate")]
    public DateTime OrderDate { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "USD";

    [JsonPropertyName("orderItems")]
    public required List<OrderItemDto> OrderItems { get; init; }

    [JsonPropertyName("shippingAddress")]
    public required AddressDto ShippingAddress { get; init; }

    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("eventTimestamp")]
    public DateTime EventTimestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("eventVersion")]
    public string EventVersion { get; init; } = "1.0";
}

/// <summary>
/// Order item information for event
/// </summary>
public record OrderItemDto
{
    [JsonPropertyName("inventoryItemId")]
    public required string InventoryItemId { get; init; }

    [JsonPropertyName("sku")]
    public required string Sku { get; init; }

    [JsonPropertyName("productName")]
    public required string ProductName { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; init; }

    [JsonPropertyName("totalPrice")]
    public decimal TotalPrice { get; init; }
}

/// <summary>
/// Address information for event
/// </summary>
public record AddressDto
{
    [JsonPropertyName("street")]
    public required string Street { get; init; }

    [JsonPropertyName("city")]
    public required string City { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("postalCode")]
    public required string PostalCode { get; init; }

    [JsonPropertyName("country")]
    public required string Country { get; init; }
}
