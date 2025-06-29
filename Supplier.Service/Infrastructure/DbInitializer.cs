using Supplier.Service.Data;

namespace Supplier.Service.Infrastructure;

public static class DbInitializer
{
    public static async Task InitializeAsync(SupplierDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Check if data already exists
        if (context.Suppliers.Any())
        {
            return; // Database has been seeded
        }

        // Seed suppliers
        var suppliers = new List<Entities.Supplier>
        {
            new()
            {
                Name = "Global Electronics Supply Co.",
                ContactPerson = "John Mitchell",
                Email = "john.mitchell@globalelectronics.com",
                Phone = "+1-555-0101",
                Address = "1234 Industrial Boulevard",
                City = "San Francisco",
                State = "California",
                PostalCode = "94102",
                Country = "United States",
                IsActive = true,
                CreatedBy = "System",
                UpdatedBy = "System"
            },
            new()
            {
                Name = "TechComponents Ltd.",
                ContactPerson = "Sarah Chen",
                Email = "sarah.chen@techcomponents.com",
                Phone = "+1-555-0202",
                Address = "5678 Technology Drive",
                City = "Austin",
                State = "Texas",
                PostalCode = "73301",
                Country = "United States",
                IsActive = true,
                CreatedBy = "System",
                UpdatedBy = "System"
            },
            new()
            {
                Name = "European Parts Distribution",
                ContactPerson = "Hans Mueller",
                Email = "h.mueller@europarts.de",
                Phone = "+49-30-12345678",
                Address = "Unter den Linden 12",
                City = "Berlin",
                State = "Berlin",
                PostalCode = "10117",
                Country = "Germany",
                IsActive = true,
                CreatedBy = "System",
                UpdatedBy = "System"
            }
        };

        await context.Suppliers.AddRangeAsync(suppliers);
        await context.SaveChangesAsync();
    }
}
