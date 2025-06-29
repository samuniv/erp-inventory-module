using Microsoft.EntityFrameworkCore;
using Supplier.Service.Entities;

namespace Supplier.Service.Data;

public class SupplierDbContext : DbContext
{
    public SupplierDbContext(DbContextOptions<SupplierDbContext> options) : base(options)
    {
    }

    public DbSet<Entities.Supplier> Suppliers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Oracle-specific conventions
        modelBuilder.Entity<Entities.Supplier>(entity =>
        {
            entity.ToTable("SUPPLIERS");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .HasColumnName("NAME")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.ContactPerson)
                .HasColumnName("CONTACT_PERSON")
                .HasMaxLength(100);

            entity.Property(e => e.Email)
                .HasColumnName("EMAIL")
                .HasMaxLength(100);

            entity.Property(e => e.Phone)
                .HasColumnName("PHONE")
                .HasMaxLength(20);

            entity.Property(e => e.Address)
                .HasColumnName("ADDRESS")
                .HasMaxLength(500);

            entity.Property(e => e.City)
                .HasColumnName("CITY")
                .HasMaxLength(100);

            entity.Property(e => e.State)
                .HasColumnName("STATE")
                .HasMaxLength(100);

            entity.Property(e => e.PostalCode)
                .HasColumnName("POSTAL_CODE")
                .HasMaxLength(20);

            entity.Property(e => e.Country)
                .HasColumnName("COUNTRY")
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .HasColumnName("IS_ACTIVE")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("SYSTIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("UPDATED_AT")
                .HasDefaultValueSql("SYSTIMESTAMP");

            entity.Property(e => e.CreatedBy)
                .HasColumnName("CREATED_BY")
                .HasMaxLength(100);

            entity.Property(e => e.UpdatedBy)
                .HasColumnName("UPDATED_BY")
                .HasMaxLength(100);

            // Add indexes for better performance
            entity.HasIndex(e => e.Name)
                .HasDatabaseName("IX_SUPPLIERS_NAME");

            entity.HasIndex(e => e.Email)
                .HasDatabaseName("IX_SUPPLIERS_EMAIL");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_SUPPLIERS_IS_ACTIVE");
        });
    }
}
