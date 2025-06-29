using Microsoft.EntityFrameworkCore;
using Order.Service.Models;

namespace Order.Service.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Models.Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Order entity
        modelBuilder.Entity<Models.Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.CustomerEmail);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.CreatedAt);

            // Configure decimal precision
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(10,2)");
            entity.Property(e => e.SubtotalAmount)
                .HasColumnType("decimal(10,2)");
            entity.Property(e => e.TaxAmount)
                .HasColumnType("decimal(8,2)");
            entity.Property(e => e.ShippingAmount)
                .HasColumnType("decimal(8,2)");

            // Configure string lengths
            entity.Property(e => e.OrderNumber).HasMaxLength(50);
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.CustomerEmail).HasMaxLength(200);
            entity.Property(e => e.CustomerPhone).HasMaxLength(15);
            entity.Property(e => e.ShippingAddress).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(50);

            // Configure enum
            entity.Property(e => e.Status)
                .HasConversion<int>();
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.InventoryItemId);
            entity.HasIndex(e => e.ItemSku);

            // Configure decimal precision
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(10,2)");
            entity.Property(e => e.LineTotal)
                .HasColumnType("decimal(10,2)");

            // Configure string lengths
            entity.Property(e => e.ItemName).HasMaxLength(100);
            entity.Property(e => e.ItemSku).HasMaxLength(50);
            entity.Property(e => e.ItemDescription).HasMaxLength(500);
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.SupplierName).HasMaxLength(200);

            // Configure relationships
            entity.HasOne(e => e.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
