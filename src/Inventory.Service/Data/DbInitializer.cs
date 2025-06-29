using Inventory.Service.Data;
using Inventory.Service.Models;

namespace Inventory.Service.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(InventoryDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // Check if data already exists
        if (context.InventoryItems.Any())
        {
            return; // Database has been seeded
        }

        var items = new[]
        {
            new InventoryItem
            {
                Name = "Laptop Computer",
                Description = "Dell Inspiron 15 3000, 8GB RAM, 256GB SSD",
                Sku = "LAP-DEL-INS-001",
                StockLevel = 25,
                ReorderThreshold = 10,
                UnitPrice = 649.99m,
                Unit = "pieces",
                Category = "Electronics",
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "Wireless Mouse",
                Description = "Logitech M705 Marathon Wireless Mouse",
                Sku = "MOU-LOG-M705-001",
                StockLevel = 50,
                ReorderThreshold = 20,
                UnitPrice = 39.99m,
                Unit = "pieces",
                Category = "Electronics",
                SupplierId = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "Office Chair",
                Description = "Ergonomic office chair with lumbar support",
                Sku = "CHR-ERG-001",
                StockLevel = 15,
                ReorderThreshold = 5,
                UnitPrice = 199.99m,
                Unit = "pieces",
                Category = "Furniture",
                SupplierId = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "A4 Paper Pack",
                Description = "500 sheets of premium A4 white paper",
                Sku = "PAP-A4-WHT-500",
                StockLevel = 8, // Low stock to trigger alert
                ReorderThreshold = 10,
                UnitPrice = 12.99m,
                Unit = "packs",
                Category = "Stationery",
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "USB-C Cable",
                Description = "2-meter USB-C to USB-C cable, fast charging",
                Sku = "CAB-USBC-2M-001",
                StockLevel = 35,
                ReorderThreshold = 15,
                UnitPrice = 19.99m,
                Unit = "pieces",
                Category = "Electronics",
                SupplierId = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "Notebook - Lined",
                Description = "A5 lined notebook, 200 pages",
                Sku = "NTB-A5-LIN-200",
                StockLevel = 3, // Low stock to trigger alert
                ReorderThreshold = 5,
                UnitPrice = 8.99m,
                Unit = "pieces",
                Category = "Stationery",
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "External Hard Drive",
                Description = "Seagate 2TB portable external hard drive",
                Sku = "HDD-SEA-2TB-001",
                StockLevel = 12,
                ReorderThreshold = 8,
                UnitPrice = 89.99m,
                Unit = "pieces",
                Category = "Electronics",
                SupplierId = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "Desk Lamp",
                Description = "LED desk lamp with adjustable brightness",
                Sku = "LMP-LED-DSK-001",
                StockLevel = 18,
                ReorderThreshold = 6,
                UnitPrice = 45.99m,
                Unit = "pieces",
                Category = "Furniture",
                SupplierId = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "Bluetooth Headphones",
                Description = "Sony WH-1000XM4 Noise Cancelling Headphones",
                Sku = "HPH-SON-WH1000-001",
                StockLevel = 0, // Out of stock to trigger critical alert
                ReorderThreshold = 5,
                UnitPrice = 299.99m,
                Unit = "pieces",
                Category = "Electronics",
                SupplierId = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Name = "Monitor Stand",
                Description = "Adjustable monitor stand for dual monitors",
                Sku = "STD-MON-DUAL-001",
                StockLevel = 22,
                ReorderThreshold = 8,
                UnitPrice = 79.99m,
                Unit = "pieces",
                Category = "Electronics",
                SupplierId = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.InventoryItems.AddRange(items);
        await context.SaveChangesAsync();
    }
}
