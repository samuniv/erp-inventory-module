using Order.Service.Models;

namespace Order.Service.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(OrderDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Check if data already exists
        if (context.Orders.Any())
        {
            return; // Database has been seeded
        }

        // Create sample orders
        var orders = new List<Models.Order>
        {
            new Models.Order
            {
                OrderNumber = "ORD-2025-001",
                CustomerName = "John Smith",
                CustomerEmail = "john.smith@email.com",
                CustomerPhone = "+1-555-0123",
                ShippingAddress = "123 Main St, Anytown, ST 12345",
                Status = OrderStatus.Delivered,
                OrderDate = DateTime.UtcNow.AddDays(-15),
                ShippedDate = DateTime.UtcNow.AddDays(-10),
                DeliveredDate = DateTime.UtcNow.AddDays(-8),
                SubtotalAmount = 150.00m,
                TaxAmount = 12.00m,
                ShippingAmount = 8.50m,
                TotalAmount = 170.50m,
                Notes = "Customer requested expedited shipping",
                CreatedBy = "system",
                UpdatedBy = "system"
            },
            new Models.Order
            {
                OrderNumber = "ORD-2025-002",
                CustomerName = "Sarah Johnson",
                CustomerEmail = "sarah.johnson@email.com",
                CustomerPhone = "+1-555-0124",
                ShippingAddress = "456 Oak Ave, Somewhere, ST 54321",
                Status = OrderStatus.Shipped,
                OrderDate = DateTime.UtcNow.AddDays(-5),
                ShippedDate = DateTime.UtcNow.AddDays(-2),
                SubtotalAmount = 89.99m,
                TaxAmount = 7.20m,
                ShippingAmount = 5.95m,
                TotalAmount = 103.14m,
                Notes = "Standard shipping",
                CreatedBy = "system",
                UpdatedBy = "system"
            },
            new Models.Order
            {
                OrderNumber = "ORD-2025-003",
                CustomerName = "Mike Wilson",
                CustomerEmail = "mike.wilson@email.com",
                CustomerPhone = "+1-555-0125",
                ShippingAddress = "789 Pine St, Elsewhere, ST 98765",
                Status = OrderStatus.Processing,
                OrderDate = DateTime.UtcNow.AddDays(-2),
                SubtotalAmount = 245.50m,
                TaxAmount = 19.64m,
                ShippingAmount = 12.75m,
                TotalAmount = 277.89m,
                Notes = "Large order - requires special handling",
                CreatedBy = "system",
                UpdatedBy = "system"
            },
            new Models.Order
            {
                OrderNumber = "ORD-2025-004",
                CustomerName = "Lisa Chen",
                CustomerEmail = "lisa.chen@email.com",
                CustomerPhone = "+1-555-0126",
                ShippingAddress = "321 Elm Dr, Newtown, ST 13579",
                Status = OrderStatus.Confirmed,
                OrderDate = DateTime.UtcNow.AddDays(-1),
                SubtotalAmount = 67.25m,
                TaxAmount = 5.38m,
                ShippingAmount = 4.99m,
                TotalAmount = 77.62m,
                Notes = "Customer pickup preferred",
                CreatedBy = "system",
                UpdatedBy = "system"
            },
            new Models.Order
            {
                OrderNumber = "ORD-2025-005",
                CustomerName = "David Brown",
                CustomerEmail = "david.brown@email.com",
                CustomerPhone = "+1-555-0127",
                ShippingAddress = "654 Maple Ln, Oldtown, ST 24680",
                Status = OrderStatus.Pending,
                OrderDate = DateTime.UtcNow,
                SubtotalAmount = 128.75m,
                TaxAmount = 10.30m,
                ShippingAmount = 7.25m,
                TotalAmount = 146.30m,
                Notes = "Awaiting payment confirmation",
                CreatedBy = "system",
                UpdatedBy = "system"
            }
        };

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync();

        // Create sample order items
        var orderItems = new List<OrderItem>
        {
            // Items for Order 1 (ORD-2025-001)
            new OrderItem
            {
                OrderId = orders[0].Id,
                InventoryItemId = 1,
                ItemName = "Office Chair",
                ItemSku = "FURN-CHAIR-001",
                ItemDescription = "Ergonomic office chair with lumbar support",
                Quantity = 2,
                UnitPrice = 75.00m,
                LineTotal = 150.00m,
                Unit = "pieces",
                Category = "Furniture",
                SupplierId = 1,
                SupplierName = "Office Furniture Co."
            },
            // Items for Order 2 (ORD-2025-002)
            new OrderItem
            {
                OrderId = orders[1].Id,
                InventoryItemId = 2,
                ItemName = "Wireless Keyboard",
                ItemSku = "TECH-KB-002",
                ItemDescription = "Bluetooth wireless keyboard",
                Quantity = 1,
                UnitPrice = 45.99m,
                LineTotal = 45.99m,
                Unit = "pieces",
                Category = "Technology",
                SupplierId = 2,
                SupplierName = "Tech Supplies Inc."
            },
            new OrderItem
            {
                OrderId = orders[1].Id,
                InventoryItemId = 3,
                ItemName = "USB Mouse",
                ItemSku = "TECH-MOUSE-003",
                ItemDescription = "Optical USB mouse",
                Quantity = 1,
                UnitPrice = 44.00m,
                LineTotal = 44.00m,
                Unit = "pieces",
                Category = "Technology",
                SupplierId = 2,
                SupplierName = "Tech Supplies Inc."
            },
            // Items for Order 3 (ORD-2025-003)
            new OrderItem
            {
                OrderId = orders[2].Id,
                InventoryItemId = 4,
                ItemName = "Standing Desk",
                ItemSku = "FURN-DESK-004",
                ItemDescription = "Height-adjustable standing desk",
                Quantity = 1,
                UnitPrice = 245.50m,
                LineTotal = 245.50m,
                Unit = "pieces",
                Category = "Furniture",
                SupplierId = 1,
                SupplierName = "Office Furniture Co."
            },
            // Items for Order 4 (ORD-2025-004)
            new OrderItem
            {
                OrderId = orders[3].Id,
                InventoryItemId = 5,
                ItemName = "Desk Lamp",
                ItemSku = "LIGHT-LAMP-005",
                ItemDescription = "LED desk lamp with adjustable brightness",
                Quantity = 1,
                UnitPrice = 67.25m,
                LineTotal = 67.25m,
                Unit = "pieces",
                Category = "Lighting",
                SupplierId = 3,
                SupplierName = "Lighting Solutions Ltd."
            },
            // Items for Order 5 (ORD-2025-005)
            new OrderItem
            {
                OrderId = orders[4].Id,
                InventoryItemId = 6,
                ItemName = "Monitor Stand",
                ItemSku = "TECH-STAND-006",
                ItemDescription = "Adjustable monitor stand with storage",
                Quantity = 2,
                UnitPrice = 64.375m,
                LineTotal = 128.75m,
                Unit = "pieces",
                Category = "Technology",
                SupplierId = 2,
                SupplierName = "Tech Supplies Inc."
            }
        };

        context.OrderItems.AddRange(orderItems);
        await context.SaveChangesAsync();
    }
}
