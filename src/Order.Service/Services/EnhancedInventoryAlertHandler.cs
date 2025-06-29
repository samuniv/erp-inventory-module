using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Order.Service.Data;
using Order.Service.Models;
using Order.Service.Events;
using Order.Service.Services;

namespace Order.Service.Services;

/// <summary>
/// Enhanced inventory alert handler with detailed business logic
/// </summary>
public class EnhancedInventoryAlertHandler : IInventoryAlertHandler
{
    private readonly ILogger<EnhancedInventoryAlertHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public EnhancedInventoryAlertHandler(
        ILogger<EnhancedInventoryAlertHandler> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<EventProcessingResult> HandleInventoryAlertAsync(InventoryAlertEvent alertEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing inventory alert for item {ItemSku}: {AlertType} - Severity: {Severity}", 
                alertEvent.Sku, alertEvent.AlertType, alertEvent.Severity);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            // Business logic based on alert type and severity
            switch (alertEvent.AlertType?.ToLowerInvariant())
            {
                case "lowstock":
                    await HandleLowStockAlert(alertEvent, context, cancellationToken);
                    break;

                case "outofstock":
                    await HandleOutOfStockAlert(alertEvent, context, cancellationToken);
                    break;

                case "restock":
                    await HandleRestockAlert(alertEvent, context, cancellationToken);
                    break;

                case "criticalstock":
                    await HandleCriticalStockAlert(alertEvent, context, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown alert type: {AlertType} for item {ItemSku}", 
                        alertEvent.AlertType, alertEvent.Sku);
                    break;
            }

            // Log the alert processing for audit purposes
            await LogAlertProcessing(alertEvent, context, cancellationToken);

            return EventProcessingResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling inventory alert for item {ItemSku}", alertEvent.Sku);
            return EventProcessingResult.Failure(ex.Message, shouldRetry: true);
        }
    }

    private async Task HandleLowStockAlert(InventoryAlertEvent alertEvent, OrderDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Processing low stock alert for item {ItemSku}: Current {Current}, Threshold {Threshold}", 
            alertEvent.Sku, alertEvent.CurrentQuantity, alertEvent.MinimumThreshold);

        // Find all pending orders that contain this inventory item
        var pendingOrders = await context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.Status == OrderStatus.Pending && 
                       o.OrderItems.Any(oi => oi.InventoryItemId == int.Parse(alertEvent.InventoryItemId)))
            .ToListAsync(cancellationToken);

        if (pendingOrders.Any())
        {
            _logger.LogInformation("Found {OrderCount} pending orders affected by low stock alert for item {ItemSku}", 
                pendingOrders.Count, alertEvent.Sku);

            // Priority-based order processing based on severity
            switch (alertEvent.Severity?.ToLowerInvariant())
            {
                case "high":
                case "critical":
                    // For high/critical severity, prioritize orders by creation date (FIFO)
                    await PrioritizeOrdersByCreationDate(pendingOrders, alertEvent, context, cancellationToken);
                    break;

                case "medium":
                    // For medium severity, add notes to orders about potential delays
                    await AddDelayNotesToOrders(pendingOrders, alertEvent, context, cancellationToken);
                    break;

                case "low":
                    // For low severity, just log for monitoring
                    _logger.LogInformation("Low severity stock alert logged for monitoring - Item {ItemSku}", alertEvent.Sku);
                    break;
            }
        }

        // Business rule: If current stock is less than 10% of threshold, escalate
        if (alertEvent.CurrentQuantity <= (alertEvent.MinimumThreshold * 0.1))
        {
            await EscalateToCriticalAlert(alertEvent, cancellationToken);
        }
    }

    private async Task HandleOutOfStockAlert(InventoryAlertEvent alertEvent, OrderDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogError("Processing out of stock alert for item {ItemSku}", alertEvent.Sku);

        // Find all pending orders with this item
        var affectedOrders = await context.Orders
            .Include(o => o.OrderItems)
            .Where(o => (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed) && 
                       o.OrderItems.Any(oi => oi.InventoryItemId == int.Parse(alertEvent.InventoryItemId)))
            .ToListAsync(cancellationToken);

        foreach (var order in affectedOrders)
        {
            // Add urgent note about out of stock item
            var existingNotes = order.Notes ?? "";
            order.Notes = $"{existingNotes}\n[URGENT] Item {alertEvent.Sku} is out of stock. Order may be delayed. Alert ID: {alertEvent.EventId}";
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = "inventory-alert-system";

            _logger.LogWarning("Updated order {OrderId} with out of stock notification for item {ItemSku}", 
                order.Id, alertEvent.Sku);
        }

        if (affectedOrders.Any())
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated {OrderCount} orders affected by out of stock alert for item {ItemSku}", 
                affectedOrders.Count, alertEvent.Sku);
        }
    }

    private async Task HandleRestockAlert(InventoryAlertEvent alertEvent, OrderDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing restock alert for item {ItemSku}: New quantity {Quantity}", 
            alertEvent.Sku, alertEvent.CurrentQuantity);

        // Find orders that were delayed due to this item being out of stock
        var delayedOrders = await context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.Status == OrderStatus.Pending && 
                       o.Notes.Contains($"Item {alertEvent.Sku} is out of stock") &&
                       o.OrderItems.Any(oi => oi.InventoryItemId == int.Parse(alertEvent.InventoryItemId)))
            .ToListAsync(cancellationToken);

        foreach (var order in delayedOrders)
        {
            // Update notes to indicate item is back in stock
            var updatedNotes = order.Notes?.Replace(
                $"Item {alertEvent.Sku} is out of stock", 
                $"Item {alertEvent.Sku} is back in stock") ?? "";
            
            order.Notes = $"{updatedNotes}\n[INFO] Item {alertEvent.Sku} restocked on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC. Order can proceed.";
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = "inventory-alert-system";

            _logger.LogInformation("Updated order {OrderId} with restock notification for item {ItemSku}", 
                order.Id, alertEvent.Sku);
        }

        if (delayedOrders.Any())
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated {OrderCount} orders with restock notification for item {ItemSku}", 
                delayedOrders.Count, alertEvent.Sku);
        }
    }

    private async Task HandleCriticalStockAlert(InventoryAlertEvent alertEvent, OrderDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogCritical("Processing CRITICAL stock alert for item {ItemSku}: Current {Current}, Threshold {Threshold}", 
            alertEvent.Sku, alertEvent.CurrentQuantity, alertEvent.MinimumThreshold);

        // For critical alerts, immediately pause new orders for this item
        // This would typically involve updating a cache or inventory availability service
        
        // Find all pending orders and mark them with critical priority
        var pendingOrders = await context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.Status == OrderStatus.Pending && 
                       o.OrderItems.Any(oi => oi.InventoryItemId == int.Parse(alertEvent.InventoryItemId)))
            .ToListAsync(cancellationToken);

        foreach (var order in pendingOrders)
        {
            order.Notes = $"{order.Notes}\n[CRITICAL] Item {alertEvent.Sku} has critical stock levels. URGENT processing required.";
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = "critical-alert-system";
        }

        if (pendingOrders.Any())
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // Log critical alert for external monitoring systems
        _logger.LogCritical("CRITICAL STOCK ALERT: Item {ItemSku} requires immediate attention. {OrderCount} orders affected.", 
            alertEvent.Sku, pendingOrders.Count);
    }

    private async Task PrioritizeOrdersByCreationDate(
        List<Models.Order> orders, 
        InventoryAlertEvent alertEvent, 
        OrderDbContext context, 
        CancellationToken cancellationToken)
    {
        var priorityOrders = orders.OrderBy(o => o.CreatedAt).ToList();
        
        for (int i = 0; i < priorityOrders.Count; i++)
        {
            var order = priorityOrders[i];
            var priority = i < 3 ? "HIGH" : "NORMAL"; // First 3 orders get high priority
            
            order.Notes = $"{order.Notes}\n[PRIORITY-{priority}] Due to low stock for item {alertEvent.Sku}, this order has been prioritized based on creation date.";
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = "priority-system";
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Prioritized {OrderCount} orders for item {ItemSku} based on creation date", 
            orders.Count, alertEvent.Sku);
    }

    private async Task AddDelayNotesToOrders(
        List<Models.Order> orders, 
        InventoryAlertEvent alertEvent, 
        OrderDbContext context, 
        CancellationToken cancellationToken)
    {
        foreach (var order in orders)
        {
            order.Notes = $"{order.Notes}\n[NOTICE] Item {alertEvent.Sku} has low stock. Potential shipping delays may occur.";
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = "alert-system";
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Added delay notices to {OrderCount} orders for item {ItemSku}", 
            orders.Count, alertEvent.Sku);
    }

    private async Task EscalateToCriticalAlert(InventoryAlertEvent alertEvent, CancellationToken cancellationToken)
    {
        _logger.LogCritical("Escalating to CRITICAL alert for item {ItemSku}: Stock below 10% of threshold", 
            alertEvent.Sku);

        // In a real system, this might trigger:
        // - Immediate notifications to procurement team
        // - Emergency purchase orders
        // - Customer notifications
        // - Executive dashboard alerts

        await Task.CompletedTask; // Placeholder for escalation logic
    }

    private async Task LogAlertProcessing(InventoryAlertEvent alertEvent, OrderDbContext context, CancellationToken cancellationToken)
    {
        // This method would typically log to an audit table
        // For now, we'll just use structured logging
        
        _logger.LogInformation("Alert processing completed for item {ItemSku}. Event ID: {EventId}, Timestamp: {Timestamp}, Severity: {Severity}", 
            alertEvent.Sku, alertEvent.EventId, alertEvent.EventTimestamp, alertEvent.Severity);

        await Task.CompletedTask;
    }
}
