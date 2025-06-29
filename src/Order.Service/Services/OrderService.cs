using Microsoft.EntityFrameworkCore;
using Order.Service.Data;
using Order.Service.DTOs;
using Order.Service.Models;
using Order.Service.Events;
using Order.Service.Services;

namespace Order.Service.Services;

/// <summary>
/// Service for order business logic
/// </summary>
public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<OrderResponse?> GetOrderAsync(int id, CancellationToken cancellationToken = default);
    Task<List<OrderResponse>> GetOrdersAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<OrderResponse?> UpdateOrderStatusAsync(int id, OrderStatus status, CancellationToken cancellationToken = default);
    Task<bool> CancelOrderAsync(int id, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of order business logic
/// </summary>
public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderDbContext context, IEventPublisher eventPublisher, ILogger<OrderService> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new order for customer: {CustomerName}", request.CustomerName);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Generate order number
            var orderNumber = await GenerateOrderNumberAsync(cancellationToken);

            // Calculate totals
            var subtotal = request.OrderItems.Sum(item => item.UnitPrice * item.Quantity);
            var taxAmount = subtotal * 0.08m; // 8% tax
            var shippingAmount = subtotal > 100 ? 0 : 15.00m; // Free shipping over $100
            var totalAmount = subtotal + taxAmount + shippingAmount;

            // Create order entity
            var order = new Models.Order
            {
                OrderNumber = orderNumber,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                ShippingAddress = request.ShippingAddress,
                Status = OrderStatus.Pending,
                SubtotalAmount = subtotal,
                TaxAmount = taxAmount,
                ShippingAmount = shippingAmount,
                TotalAmount = totalAmount,
                Notes = request.Notes,
                OrderDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "api",
                UpdatedBy = "api"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            // Create order items
            foreach (var itemRequest in request.OrderItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    InventoryItemId = itemRequest.InventoryItemId,
                    ItemName = itemRequest.ItemName,
                    ItemSku = itemRequest.ItemSku,
                    ItemDescription = itemRequest.ItemDescription,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = itemRequest.UnitPrice,
                    LineTotal = itemRequest.UnitPrice * itemRequest.Quantity,
                    Unit = itemRequest.Unit,
                    Category = itemRequest.Category,
                    SupplierId = itemRequest.SupplierId,
                    SupplierName = itemRequest.SupplierName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.OrderItems.Add(orderItem);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Load the complete order with items for event publishing
            var createdOrder = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstAsync(o => o.Id == order.Id, cancellationToken);

            // Publish order created event
            var orderCreatedEvent = EventMapper.ToOrderCreatedEvent(createdOrder);
            var publishResult = await _eventPublisher.PublishAsync(
                KafkaTopics.OrderCreated, 
                orderCreatedEvent, 
                createdOrder.Id.ToString(), 
                cancellationToken);

            if (!publishResult.IsSuccess)
            {
                _logger.LogWarning("Failed to publish OrderCreatedEvent for order {OrderId}: {Error}", 
                    createdOrder.Id, publishResult.ErrorMessage);
                // Note: We don't fail the transaction for event publishing failures
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully created order {OrderId} with order number {OrderNumber}", 
                createdOrder.Id, createdOrder.OrderNumber);

            return MapToOrderResponse(createdOrder);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create order for customer: {CustomerName}", request.CustomerName);
            throw;
        }
    }

    public async Task<OrderResponse?> GetOrderAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order != null ? MapToOrderResponse(order) : null;
    }

    public async Task<List<OrderResponse>> GetOrdersAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToOrderResponse).ToList();
    }

    public async Task<OrderResponse?> UpdateOrderStatusAsync(int id, OrderStatus status, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
            return null;

        var oldStatus = order.Status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = "api";

        // Set specific dates based on status
        switch (status)
        {
            case OrderStatus.Shipped:
                order.ShippedDate = DateTime.UtcNow;
                break;
            case OrderStatus.Delivered:
                order.DeliveredDate = DateTime.UtcNow;
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Publish status change event
        if (status == OrderStatus.Delivered)
        {
            var completedEvent = EventMapper.ToOrderCompletedEvent(order);
            await _eventPublisher.PublishAsync(
                KafkaTopics.OrderCompleted, 
                completedEvent, 
                order.Id.ToString(), 
                cancellationToken);
        }

        _logger.LogInformation("Updated order {OrderId} status from {OldStatus} to {NewStatus}", 
            id, oldStatus, status);

        return MapToOrderResponse(order);
    }

    public async Task<bool> CancelOrderAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null || order.Status == OrderStatus.Cancelled)
            return false;

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
        {
            throw new InvalidOperationException($"Cannot cancel order {id} with status {order.Status}");
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = "api";

        await _context.SaveChangesAsync(cancellationToken);

        // Publish cancelled event
        var cancelledEvent = EventMapper.ToOrderCancelledEvent(order, reason);
        await _eventPublisher.PublishAsync(
            KafkaTopics.OrderCancelled, 
            cancelledEvent, 
            order.Id.ToString(), 
            cancellationToken);

        _logger.LogInformation("Cancelled order {OrderId} with reason: {Reason}", id, reason);

        return true;
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await _context.Orders
            .CountAsync(o => o.OrderNumber.StartsWith($"ORD-{today}"), cancellationToken);
        
        return $"ORD-{today}-{(count + 1):D4}";
    }

    private static OrderResponse MapToOrderResponse(Models.Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            ShippingAddress = order.ShippingAddress,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            TaxAmount = order.TaxAmount,
            ShippingAmount = order.ShippingAmount,
            SubtotalAmount = order.SubtotalAmount,
            Notes = order.Notes,
            OrderDate = order.OrderDate,
            ShippedDate = order.ShippedDate,
            DeliveredDate = order.DeliveredDate,
            CreatedAt = order.CreatedAt,
            OrderItems = order.OrderItems.Select(item => new OrderItemResponse
            {
                Id = item.Id,
                InventoryItemId = item.InventoryItemId,
                ItemName = item.ItemName,
                ItemSku = item.ItemSku,
                ItemDescription = item.ItemDescription,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                Unit = item.Unit,
                Category = item.Category,
                SupplierId = item.SupplierId,
                SupplierName = item.SupplierName
            }).ToList()
        };
    }
}
