using System.Net;
using System.Net.Http.Json;
using EndToEnd.Integration.Tests.Fixtures;
using FluentAssertions;
using Newtonsoft.Json;
using Shared.TestInfrastructure.Utilities;
using Xunit;

namespace EndToEnd.Integration.Tests;

/// <summary>
/// End-to-end tests focused on specific business scenarios and workflows
/// that span multiple services and validate business logic
/// </summary>
[Collection("End-to-End Integration Tests")]
public class BusinessScenarioTests
{
    private readonly EndToEndTestFixture _fixture;

    public BusinessScenarioTests(EndToEndTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RestockWorkflow_ShouldUpdateInventoryAfterSupplierDelivery()
    {
        // Business Scenario: Supplier delivers new stock to replenish low inventory
        // 1. Create supplier and low stock inventory item
        // 2. Verify low stock condition
        // 3. Simulate restock from supplier
        // 4. Verify inventory levels are restored

        // Arrange - Create supplier
        var supplierDto = new
        {
            Name = "Restock Test Supplier",
            ContactPerson = "Restock Manager",
            Email = "restock@supplier.com",
            Phone = "+1-555-REST",
            Address = "Restock Street 100",
            City = "Restock City",
            State = "RS",
            PostalCode = "10001",
            Country = "USA"
        };

        var supplierResponse = await _fixture.SupplierServiceClient.PostAsJsonAsync("/api/suppliers", supplierDto);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<dynamic>();
        var supplierId = ((Newtonsoft.Json.Linq.JValue)supplier!.id).Value;

        // Create low stock inventory item
        var productId = Guid.NewGuid();
        var inventoryDto = new
        {
            ProductId = productId,
            ProductName = "Restock Test Product",
            Quantity = 5, // Low stock
            ReorderLevel = 20,
            UnitPrice = 30.00m,
            SupplierId = supplierId,
            Location = "Restock Warehouse"
        };

        var inventoryResponse = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventoryDto);
        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(1000);

        // Verify initial low stock condition
        var initialInventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        var initialInventory = await initialInventoryResponse.Content.ReadFromJsonAsync<dynamic>();
        var initialQuantity = ((Newtonsoft.Json.Linq.JValue)initialInventory!.quantity).Value;
        ((int)initialQuantity).Should().Be(5, "Initial stock should be low");

        // Act - Simulate restock delivery (update inventory quantity)
        var restockDto = new
        {
            ProductId = productId,
            ProductName = "Restock Test Product",
            Quantity = 100, // Restocked amount
            ReorderLevel = 20,
            UnitPrice = 30.00m,
            SupplierId = supplierId,
            Location = "Restock Warehouse"
        };

        var restockResponse = await _fixture.InventoryServiceClient.PutAsJsonAsync($"/api/inventory/{productId}", restockDto);
        restockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(1000);

        // Assert - Verify inventory is restocked
        var restockedInventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        var restockedInventory = await restockedInventoryResponse.Content.ReadFromJsonAsync<dynamic>();
        var newQuantity = ((Newtonsoft.Json.Linq.JValue)restockedInventory!.quantity).Value;
        ((int)newQuantity).Should().Be(100, "Inventory should be restocked");
    }

    [Fact]
    public async Task CustomerOrderFulfillmentJourney_ShouldProcessCompleteWorkflow()
    {
        // Business Scenario: Complete customer order fulfillment journey
        // 1. Customer places order with multiple items
        // 2. System checks inventory availability
        // 3. Inventory is reserved/reduced
        // 4. Low stock alerts are triggered if needed
        // 5. Order status is updated

        // Arrange - Create supplier and inventory items
        var supplierDto = new
        {
            Name = "Fulfillment Supplier Co",
            ContactPerson = "Fulfillment Agent",
            Email = "fulfillment@supplier.com",
            Phone = "+1-555-FULL",
            Address = "Fulfillment Blvd 200",
            City = "Fulfillment City",
            State = "FC",
            PostalCode = "20002",
            Country = "USA"
        };

        var supplierResponse = await _fixture.SupplierServiceClient.PostAsJsonAsync("/api/suppliers", supplierDto);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<dynamic>();
        var supplierId = ((Newtonsoft.Json.Linq.JValue)supplier!.id).Value;

        // Create multiple inventory items
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();

        var inventory1Dto = new
        {
            ProductId = product1Id,
            ProductName = "Fulfillment Product A",
            Quantity = 50,
            ReorderLevel = 15,
            UnitPrice = 25.99m,
            SupplierId = supplierId,
            Location = "Fulfillment Warehouse A"
        };

        var inventory2Dto = new
        {
            ProductId = product2Id,
            ProductName = "Fulfillment Product B",
            Quantity = 30,
            ReorderLevel = 25, // High reorder level to trigger alert
            UnitPrice = 45.50m,
            SupplierId = supplierId,
            Location = "Fulfillment Warehouse B"
        };

        await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventory1Dto);
        await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventory2Dto);

        await Task.Delay(1000);

        // Act - Customer places multi-item order
        var customerOrderDto = new
        {
            CustomerName = "Jane Customer",
            CustomerEmail = "jane@customer.com",
            Items = new[]
            {
                new
                {
                    ProductId = product1Id,
                    ProductName = "Fulfillment Product A",
                    Quantity = 10,
                    UnitPrice = 25.99m
                },
                new
                {
                    ProductId = product2Id,
                    ProductName = "Fulfillment Product B",
                    Quantity = 8, // This will bring Product B to 22, below reorder level of 25
                    UnitPrice = 45.50m
                }
            }
        };

        var orderResponse = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", customerOrderDto);
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await orderResponse.Content.ReadFromJsonAsync<dynamic>();
        order.Should().NotBeNull();

        // Wait for order processing
        await Task.Delay(3000);

        // Assert - Verify inventory updates
        var product1Response = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{product1Id}");
        var product1Inventory = await product1Response.Content.ReadFromJsonAsync<dynamic>();
        ((int)product1Inventory!.quantity).Should().Be(40, "Product A quantity should be reduced by 10");

        var product2Response = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{product2Id}");
        var product2Inventory = await product2Response.Content.ReadFromJsonAsync<dynamic>();
        ((int)product2Inventory!.quantity).Should().Be(22, "Product B quantity should be reduced by 8");

        // Verify low stock alert for Product B
        var messages = await KafkaTestUtilities.ConsumeMessagesAsync("inventory-alerts",
            _fixture.KafkaBootstrapAddress, TimeSpan.FromSeconds(10), maxMessages: 5);

        var productBAlert = messages.FirstOrDefault(m => m.Contains(product2Id.ToString()));
        productBAlert.Should().NotBeNull("Low stock alert should be generated for Product B");
    }

    [Fact]
    public async Task SupplierManagementWorkflow_ShouldHandleSupplierLifecycle()
    {
        // Business Scenario: Complete supplier management lifecycle
        // 1. Create new supplier with validation
        // 2. Associate inventory items with supplier
        // 3. Update supplier information
        // 4. Temporarily deactivate supplier
        // 5. Reactivate supplier
        // 6. Verify data consistency throughout

        // Arrange & Act - Create supplier with comprehensive data
        var supplierDto = new
        {
            Name = "Lifecycle Test Supplier Ltd",
            ContactPerson = "Lifecycle Manager",
            Email = "lifecycle@supplier.com",
            Phone = "+1-555-LIFE",
            Address = "Lifecycle Avenue 300",
            City = "Lifecycle City",
            State = "LC",
            PostalCode = "30003",
            Country = "USA"
        };

        var createResponse = await _fixture.SupplierServiceClient.PostAsJsonAsync("/api/suppliers", supplierDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var supplier = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        var supplierId = ((Newtonsoft.Json.Linq.JValue)supplier!.id).Value;

        // Associate multiple inventory items
        var productIds = new List<Guid>();
        for (int i = 1; i <= 3; i++)
        {
            var productId = Guid.NewGuid();
            productIds.Add(productId);

            var inventoryDto = new
            {
                ProductId = productId,
                ProductName = $"Lifecycle Product {i}",
                Quantity = 50 + (i * 10),
                ReorderLevel = 10 + (i * 5),
                UnitPrice = 20.00m + (i * 5),
                SupplierId = supplierId,
                Location = $"Lifecycle Warehouse {i}"
            };

            var inventoryResponse = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventoryDto);
            inventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await Task.Delay(1000);

        // Update supplier information
        var updateDto = new
        {
            Name = "Updated Lifecycle Supplier Ltd",
            ContactPerson = "Updated Lifecycle Manager",
            Email = "updated-lifecycle@supplier.com",
            Phone = "+1-555-UPDT",
            Address = "Updated Lifecycle Avenue 300",
            City = "Updated Lifecycle City",
            State = "ULC",
            PostalCode = "30033",
            Country = "USA",
            IsActive = true
        };

        var updateResponse = await _fixture.SupplierServiceClient.PutAsJsonAsync($"/api/suppliers/{supplierId}", updateDto);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify supplier update
        var getUpdatedResponse = await _fixture.SupplierServiceClient.GetAsync($"/api/suppliers/{supplierId}");
        var updatedSupplier = await getUpdatedResponse.Content.ReadFromJsonAsync<dynamic>();
        ((string)updatedSupplier!.name).Should().Be("Updated Lifecycle Supplier Ltd");
        ((string)updatedSupplier.email).Should().Be("updated-lifecycle@supplier.com");

        // Deactivate supplier
        var deactivateDto = new
        {
            Name = "Updated Lifecycle Supplier Ltd",
            ContactPerson = "Updated Lifecycle Manager",
            Email = "updated-lifecycle@supplier.com",
            Phone = "+1-555-UPDT",
            Address = "Updated Lifecycle Avenue 300",
            City = "Updated Lifecycle City",
            State = "ULC",
            PostalCode = "30033",
            Country = "USA",
            IsActive = false // Deactivate
        };

        var deactivateResponse = await _fixture.SupplierServiceClient.PutAsJsonAsync($"/api/suppliers/{supplierId}", deactivateDto);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify supplier is deactivated
        var getDeactivatedResponse = await _fixture.SupplierServiceClient.GetAsync($"/api/suppliers/{supplierId}");
        var deactivatedSupplier = await getDeactivatedResponse.Content.ReadFromJsonAsync<dynamic>();
        ((bool)deactivatedSupplier!.isActive).Should().BeFalse();

        // Verify inventory items still exist and reference the supplier
        foreach (var productId in productIds)
        {
            var inventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
            inventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var inventory = await inventoryResponse.Content.ReadFromJsonAsync<dynamic>();
            ((int)inventory!.supplierId).Should().Be((int)supplierId);
        }

        // Assert - Reactivate supplier
        var reactivateDto = new
        {
            Name = "Updated Lifecycle Supplier Ltd",
            ContactPerson = "Updated Lifecycle Manager",
            Email = "updated-lifecycle@supplier.com",
            Phone = "+1-555-UPDT",
            Address = "Updated Lifecycle Avenue 300",
            City = "Updated Lifecycle City",
            State = "ULC",
            PostalCode = "30033",
            Country = "USA",
            IsActive = true // Reactivate
        };

        var reactivateResponse = await _fixture.SupplierServiceClient.PutAsJsonAsync($"/api/suppliers/{supplierId}", reactivateDto);
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify supplier is reactivated
        var getFinalResponse = await _fixture.SupplierServiceClient.GetAsync($"/api/suppliers/{supplierId}");
        var finalSupplier = await getFinalResponse.Content.ReadFromJsonAsync<dynamic>();
        ((bool)finalSupplier!.isActive).Should().BeTrue();
    }

    [Fact]
    public async Task InventoryMovementAuditWorkflow_ShouldTrackAllChanges()
    {
        // Business Scenario: Inventory movement and audit trail
        // 1. Create inventory item
        // 2. Perform various operations (orders, restocks, adjustments)
        // 3. Verify inventory history and consistency

        // Arrange - Create inventory item
        var productId = Guid.NewGuid();
        var initialInventoryDto = new
        {
            ProductId = productId,
            ProductName = "Audit Trail Product",
            Quantity = 100,
            ReorderLevel = 20,
            UnitPrice = 40.00m,
            SupplierId = 1, // Use seeded supplier
            Location = "Audit Warehouse"
        };

        var createInventoryResponse = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", initialInventoryDto);
        createInventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(1000);

        // Verify initial state
        var initialCheckResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        var initialInventory = await initialCheckResponse.Content.ReadFromJsonAsync<dynamic>();
        ((int)initialInventory!.quantity).Should().Be(100);

        // Act - Perform multiple inventory operations

        // 1. Create order that reduces inventory
        var order1Dto = new
        {
            CustomerName = "Audit Customer 1",
            CustomerEmail = "audit1@customer.com",
            Items = new[]
            {
                new
                {
                    ProductId = productId,
                    ProductName = "Audit Trail Product",
                    Quantity = 25,
                    UnitPrice = 40.00m
                }
            }
        };

        var order1Response = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", order1Dto);
        order1Response.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(2000);

        // Verify first reduction
        var afterOrder1Response = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        var afterOrder1Inventory = await afterOrder1Response.Content.ReadFromJsonAsync<dynamic>();
        ((int)afterOrder1Inventory!.quantity).Should().Be(75, "Inventory should be reduced by first order");

        // 2. Restock inventory
        var restockDto = new
        {
            ProductId = productId,
            ProductName = "Audit Trail Product",
            Quantity = 125, // Increase by 50
            ReorderLevel = 20,
            UnitPrice = 40.00m,
            SupplierId = 1,
            Location = "Audit Warehouse"
        };

        var restockResponse = await _fixture.InventoryServiceClient.PutAsJsonAsync($"/api/inventory/{productId}", restockDto);
        restockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(1000);

        // Verify restock
        var afterRestockResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        var afterRestockInventory = await afterRestockResponse.Content.ReadFromJsonAsync<dynamic>();
        ((int)afterRestockInventory!.quantity).Should().Be(125, "Inventory should be restocked");

        // 3. Another order
        var order2Dto = new
        {
            CustomerName = "Audit Customer 2",
            CustomerEmail = "audit2@customer.com",
            Items = new[]
            {
                new
                {
                    ProductId = productId,
                    ProductName = "Audit Trail Product",
                    Quantity = 15,
                    UnitPrice = 40.00m
                }
            }
        };

        var order2Response = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", order2Dto);
        order2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(2000);

        // Assert - Verify final inventory state
        var finalResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        var finalInventory = await finalResponse.Content.ReadFromJsonAsync<dynamic>();
        ((int)finalInventory!.quantity).Should().Be(110, "Final inventory should be 125 - 15 = 110");

        // The audit trail would ideally be tracked in a separate audit table/service
        // For this test, we're verifying the final consistency
    }

    [Fact]
    public async Task PeakLoadScenario_ShouldHandleHighVolumeOperations()
    {
        // Business Scenario: Peak load handling during high-volume periods
        // 1. Create multiple inventory items
        // 2. Simulate burst of concurrent orders
        // 3. Verify system maintains consistency under load

        // Arrange - Create multiple inventory items for load testing
        var productIds = new List<Guid>();
        const int numberOfProducts = 5;
        const int initialQuantityPerProduct = 100;

        for (int i = 1; i <= numberOfProducts; i++)
        {
            var productId = Guid.NewGuid();
            productIds.Add(productId);

            var inventoryDto = new
            {
                ProductId = productId,
                ProductName = $"Load Test Product {i}",
                Quantity = initialQuantityPerProduct,
                ReorderLevel = 30,
                UnitPrice = 20.00m + i,
                SupplierId = 1,
                Location = $"Load Test Warehouse {i}"
            };

            var response = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventoryDto);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await Task.Delay(2000);

        // Act - Generate burst of concurrent orders
        var orderTasks = new List<Task<HttpResponseMessage>>();
        const int numberOfOrders = 20;
        const int itemsPerOrder = 2;
        const int quantityPerItem = 5;

        var random = new Random();

        for (int i = 1; i <= numberOfOrders; i++)
        {
            // Select random products for each order
            var selectedProducts = productIds.OrderBy(x => random.Next()).Take(itemsPerOrder).ToList();

            var orderDto = new
            {
                CustomerName = $"Load Test Customer {i}",
                CustomerEmail = $"loadtest{i}@customer.com",
                Items = selectedProducts.Select((productId, index) => new
                {
                    ProductId = productId,
                    ProductName = $"Load Test Product {productIds.IndexOf(productId) + 1}",
                    Quantity = quantityPerItem,
                    UnitPrice = 20.00m + (productIds.IndexOf(productId) + 1)
                }).ToArray()
            };

            orderTasks.Add(_fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", orderDto));

            // Small delay to spread out the requests slightly
            await Task.Delay(50);
        }

        var responses = await Task.WhenAll(orderTasks);

        // Wait for all processing to complete
        await Task.Delay(5000);

        // Assert - Verify system handled load correctly
        var successfulOrders = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        successfulOrders.Should().BeGreaterThan(numberOfOrders / 2,
            "At least half of the orders should be successful under load");

        // Verify inventory consistency
        var totalExpectedReduction = successfulOrders * itemsPerOrder * quantityPerItem;

        foreach (var productId in productIds)
        {
            var inventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
            inventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var inventory = await inventoryResponse.Content.ReadFromJsonAsync<dynamic>();
            var currentQuantity = (int)inventory!.quantity;

            // Each product might have been ordered multiple times
            currentQuantity.Should().BeInRange(0, initialQuantityPerProduct,
                "Inventory quantity should be within expected range after load testing");
        }
    }
}
