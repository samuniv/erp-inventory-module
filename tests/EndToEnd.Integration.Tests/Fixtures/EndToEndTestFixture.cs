using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Shared.TestInfrastructure.Fixtures;
using Shared.TestInfrastructure.Utilities;
using Confluent.Kafka;
using System.Net.Http.Json;
using Xunit;
using System.Reflection;

namespace EndToEnd.Integration.Tests.Fixtures;

/// <summary>
/// End-to-end test fixture that orchestrates multiple services together
/// for comprehensive cross-service integration testing
/// </summary>
public class EndToEndTestFixture : IAsyncLifetime
{
    public IntegrationTestFixture Infrastructure { get; private set; } = null!;

    // HTTP clients for each service using direct HTTP calls
    public HttpClient OrderServiceClient { get; private set; } = null!;
    public HttpClient InventoryServiceClient { get; private set; } = null!;
    public HttpClient SupplierServiceClient { get; private set; } = null!;

    // Kafka bootstrap address for utilities
    public string KafkaBootstrapAddress { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Initialize shared infrastructure (PostgreSQL, Oracle, Kafka)
        Infrastructure = new IntegrationTestFixture();
        await Infrastructure.InitializeAsync();

        // Store Kafka bootstrap address for utility methods
        KafkaBootstrapAddress = Infrastructure.Kafka.BootstrapServers;

        // Create HTTP clients that will connect to running services
        // For now, we'll assume services are running on standard ports
        // In a real scenario, you'd want to start the services programmatically

        var handler = new HttpClientHandler();
        OrderServiceClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5001") // Assume Order Service runs on 5001
        };

        InventoryServiceClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5002") // Assume Inventory Service runs on 5002
        };

        SupplierServiceClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5003") // Assume Supplier Service runs on 5003
        };

        // Wait for services to be ready (simplified check)
        await Task.Delay(2000);

        // Seed test data
        await SeedTestData();
    }

    private async Task SeedTestData()
    {
        // Seed suppliers
        await SeedSuppliersAsync();

        // Seed inventory items
        await SeedInventoryAsync();

        // Allow time for seeding to complete
        await Task.Delay(2000);
    }

    private async Task SeedSuppliersAsync()
    {
        var suppliers = new[]
        {
            new {
                Name = "E2E Electronics Corp",
                ContactPerson = "John E2E",
                Email = "john@e2e-electronics.com",
                Phone = "+1-555-0001",
                Address = "123 E2E Street",
                City = "E2E City",
                State = "E2E State",
                PostalCode = "12345",
                Country = "USA"
            },
            new {
                Name = "E2E Manufacturing Ltd",
                ContactPerson = "Jane E2E",
                Email = "jane@e2e-manufacturing.com",
                Phone = "+1-555-0002",
                Address = "456 Manufacturing Ave",
                City = "Manufacturing City",
                State = "MFG State",
                PostalCode = "54321",
                Country = "USA"
            }
        };

        foreach (var supplier in suppliers)
        {
            try
            {
                await SupplierServiceClient.PostAsJsonAsync("/api/suppliers", supplier);
            }
            catch
            {
                // Ignore seeding errors for now
            }
        }
    }

    private async Task SeedInventoryAsync()
    {
        var inventoryItems = new[]
        {
            new {
                ProductId = Guid.NewGuid(),
                ProductName = "E2E Test Widget",
                Quantity = 100,
                ReorderLevel = 20,
                UnitPrice = 25.99m,
                SupplierId = 1,
                Location = "Warehouse A"
            },
            new {
                ProductId = Guid.NewGuid(),
                ProductName = "E2E Test Gadget",
                Quantity = 50,
                ReorderLevel = 10,
                UnitPrice = 15.50m,
                SupplierId = 2,
                Location = "Warehouse B"
            }
        };

        foreach (var item in inventoryItems)
        {
            try
            {
                await InventoryServiceClient.PostAsJsonAsync("/api/inventory", item);
            }
            catch
            {
                // Ignore seeding errors for now
            }
        }
    }

    public async Task DisposeAsync()
    {
        OrderServiceClient?.Dispose();
        InventoryServiceClient?.Dispose();
        SupplierServiceClient?.Dispose();

        if (Infrastructure != null)
        {
            await Infrastructure.DisposeAsync();
        }
    }
}
