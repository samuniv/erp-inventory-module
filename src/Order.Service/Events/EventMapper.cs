using Order.Service.Models;
using Order.Service.Events;

namespace Order.Service.Events;

/// <summary>
/// Maps between domain entities and event contracts
/// </summary>
public static class EventMapper
{
    /// <summary>
    /// Maps an Order entity to OrderCreatedEvent
    /// </summary>
    public static OrderCreatedEvent ToOrderCreatedEvent(Models.Order order, string? correlationId = null)
    {
        return new OrderCreatedEvent
        {
            OrderId = order.Id.ToString(),
            OrderNumber = order.OrderNumber,
            CustomerId = order.Id.ToString(), // Using OrderId as CustomerId for now
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Currency = "USD", // Default currency
            OrderItems = order.OrderItems.Select(ToOrderItemDto).ToList(),
            ShippingAddress = ToAddressDto(order),
            EventId = Guid.NewGuid().ToString(),
            EventTimestamp = DateTime.UtcNow,
            EventVersion = "1.0"
        };
    }

    /// <summary>
    /// Maps an Order entity to OrderCompletedEvent
    /// </summary>
    public static OrderCompletedEvent ToOrderCompletedEvent(Models.Order order, string? correlationId = null)
    {
        return new OrderCompletedEvent
        {
            OrderId = order.Id.ToString(),
            OrderNumber = order.OrderNumber,
            CustomerId = order.Id.ToString(), // Using OrderId as CustomerId for now
            CompletedDate = DateTime.UtcNow,
            TotalAmount = order.TotalAmount,
            OrderItems = order.OrderItems.Select(ToOrderItemDto).ToList(),
            EventId = Guid.NewGuid().ToString(),
            EventTimestamp = DateTime.UtcNow,
            EventVersion = "1.0",
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Maps an Order entity to OrderCancelledEvent
    /// </summary>
    public static OrderCancelledEvent ToOrderCancelledEvent(Models.Order order, string reason, string? correlationId = null)
    {
        return new OrderCancelledEvent
        {
            OrderId = order.Id.ToString(),
            OrderNumber = order.OrderNumber,
            CustomerId = order.Id.ToString(), // Using OrderId as CustomerId for now
            CancelledDate = DateTime.UtcNow,
            Reason = reason,
            OrderItems = order.OrderItems.Select(ToOrderItemDto).ToList(),
            EventId = Guid.NewGuid().ToString(),
            EventTimestamp = DateTime.UtcNow,
            EventVersion = "1.0",
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Maps an OrderItem entity to OrderItemDto
    /// </summary>
    private static OrderItemDto ToOrderItemDto(OrderItem orderItem)
    {
        return new OrderItemDto
        {
            InventoryItemId = orderItem.InventoryItemId.ToString(),
            Sku = orderItem.ItemSku,
            ProductName = orderItem.ItemName,
            Quantity = orderItem.Quantity,
            UnitPrice = orderItem.UnitPrice,
            TotalPrice = orderItem.LineTotal
        };
    }

    /// <summary>
    /// Maps Order shipping address to AddressDto
    /// </summary>
    private static AddressDto ToAddressDto(Models.Order order)
    {
        // Parse the shipping address string (simplified approach)
        var address = order.ShippingAddress;
        var parts = address.Split(',').Select(p => p.Trim()).ToArray();
        
        return new AddressDto
        {
            Street = parts.Length > 0 ? parts[0] : "",
            City = parts.Length > 1 ? parts[1] : "",
            State = parts.Length > 2 ? parts[2] : "",
            PostalCode = parts.Length > 3 ? parts[3] : "",
            Country = parts.Length > 4 ? parts[4] : "USA"
        };
    }
}
