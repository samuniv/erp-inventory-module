using System.Net;
using System.Net.Http.Json;
using EndToEnd.Integration.Tests.Fixtures;
using FluentAssertions;
using Confluent.Kafka;
using Newtonsoft.Json;
using Shared.TestInfrastructure.Utilities;
using Xunit;

namespace EndToEnd.Integration.Tests;

/// <summary>
/// End-to-end integration tests that validate complete workflows across multiple services
/// These tests simulate real-world scenarios involving Order, Inventory, and Supplier services
/// </summary>
[Collection("End-to-End Integration Tests")]
public class CrossServiceWorkflowTests
{
    private readonly EndToEndTestFixture _fixture;

    public CrossServiceWorkflowTests(EndToEndTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteOrderWorkflow_ShouldProcessOrderThroughAllServices()
    {
        // This test validates the complete order processing workflow:
        // 1. Create a supplier
        // 2. Create inventory items linked to the supplier
        // 3. Create an order that consumes inventory
        // 4. Verify inventory is updated
        // 5. Verify low stock alerts are generated via Kafka

        // Arrange - Create a supplier
        var supplierDto = new
        {
            Name = "Complete Workflow Supplier",
            ContactPerson = "Workflow Manager",
            Email = "workflow@supplier.com",
            Phone = "+1-555-WRKF",
            Address = "Workflow Street 123",
            City = "Workflow City",
            State = "Workflow State",
            PostalCode = "12345",
            Country = "USA"
        };

        var supplierResponse = await _fixture.SupplierServiceClient.PostAsJsonAsync("/api/suppliers", supplierDto);
        supplierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<dynamic>();
        var supplierId = ((Newtonsoft.Json.Linq.JValue)supplier!.id).Value;

        // Create inventory item linked to supplier
        var inventoryDto = new
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Workflow Test Product",
            Quantity = 50,
            ReorderLevel = 10,
            UnitPrice = 29.99m,
            SupplierId = supplierId,
            Location = "Workflow Warehouse"
        };

        var inventoryResponse = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventoryDto);
        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var inventoryItem = await inventoryResponse.Content.ReadFromJsonAsync<dynamic>();
        var productId = ((Newtonsoft.Json.Linq.JValue)inventoryItem!.productId).Value;

        // Wait for inventory to be fully created
        await Task.Delay(1000);

        // Act - Create an order that will trigger inventory reduction
        var orderDto = new
        {
            CustomerName = "Workflow Customer",
            CustomerEmail = "customer@workflow.com",
            Items = new[]
            {
                new
                {
                    ProductId = productId,
                    ProductName = "Workflow Test Product",
                    Quantity = 15,
                    UnitPrice = 29.99m
                }
            }
        };

        var orderResponse = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", orderDto);

        // Assert - Verify order was created successfully
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await orderResponse.Content.ReadFromJsonAsync<dynamic>();
        order.Should().NotBeNull();

        // Wait for order processing and Kafka messages
        await Task.Delay(3000);

        // Verify inventory was updated
        var updatedInventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        updatedInventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedInventory = await updatedInventoryResponse.Content.ReadFromJsonAsync<dynamic>();
        
        var updatedQuantity = ((Newtonsoft.Json.Linq.JValue)updatedInventory!.quantity).Value;
        updatedQuantity.Should().Be(35, "Inventory should be reduced by order quantity");

        // Verify low stock alert was generated (quantity 35 is above reorder level 10, so no alert expected)
        // But if we create another larger order, we should get an alert
        var largeOrderDto = new
        {
            CustomerName = "Large Order Customer",
            CustomerEmail = "large@customer.com",
            Items = new[]
            {
                new
                {
                    ProductId = productId,
                    ProductName = "Workflow Test Product",
                    Quantity = 30, // This should bring inventory to 5, below reorder level of 10
                    UnitPrice = 29.99m
                }
            }
        };

        var largeOrderResponse = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", largeOrderDto);
        largeOrderResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Wait for Kafka message processing
        await Task.Delay(5000);

        // Check for low stock alert via Kafka
        var messages = await KafkaTestUtilities.ConsumeMessagesAsync("inventory-alerts", 
            _fixture.KafkaBootstrapAddress, TimeSpan.FromSeconds(10), maxMessages: 5);
        
        messages.Should().NotBeEmpty("Low stock alert should be published to Kafka");
        
        var lowStockAlert = messages.FirstOrDefault(m => 
            m.Contains(productId.ToString()) && m.Contains("low stock"));
        lowStockAlert.Should().NotBeNull("Should receive low stock alert for the product");
    }

    [Fact]
    public async Task SupplierInventoryWorkflow_ShouldMaintainDataConsistency()
    {
        // This test validates data consistency between Supplier and Inventory services:
        // 1. Create a supplier
        // 2. Create multiple inventory items for that supplier
        // 3. Update supplier information
        // 4. Verify inventory items still reference the supplier correctly
        // 5. Deactivate supplier and verify business rules

        // Arrange & Act - Create supplier
        var supplierDto = new
        {
            Name = "Data Consistency Supplier",
            ContactPerson = "Consistency Manager",
            Email = "consistency@supplier.com",
            Phone = "+1-555-CONS",
            Address = "Consistency Ave 456",
            City = "Consistency City",
            State = "CS",
            PostalCode = "67890",
            Country = "USA"
        };

        var supplierResponse = await _fixture.SupplierServiceClient.PostAsJsonAsync("/api/suppliers", supplierDto);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<dynamic>();
        var supplierId = ((Newtonsoft.Json.Linq.JValue)supplier!.id).Value;

        // Create multiple inventory items for this supplier
        var inventoryItems = new[]
        {
            new
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Consistency Product A",
                Quantity = 100,
                ReorderLevel = 25,
                UnitPrice = 19.99m,
                SupplierId = supplierId,
                Location = "Warehouse C1"
            },
            new
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Consistency Product B",
                Quantity = 75,
                ReorderLevel = 15,
                UnitPrice = 34.50m,
                SupplierId = supplierId,
                Location = "Warehouse C2"
            }
        };

        var createdProductIds = new List<object>();
        foreach (var item in inventoryItems)
        {
            var response = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", item);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await response.Content.ReadFromJsonAsync<dynamic>();
            createdProductIds.Add(((Newtonsoft.Json.Linq.JValue)created!.productId).Value);
        }

        await Task.Delay(1000);

        // Update supplier information
        var updateSupplierDto = new
        {
            Name = "Updated Consistency Supplier",
            ContactPerson = "Updated Manager",
            Email = "updated@supplier.com",
            Phone = "+1-555-UPDT",
            Address = "Updated Address 789",
            City = "Updated City",
            State = "UP",
            PostalCode = "98765",
            Country = "USA",
            IsActive = true
        };

        var updateResponse = await _fixture.SupplierServiceClient.PutAsJsonAsync($"/api/suppliers/{supplierId}", updateSupplierDto);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Verify data consistency
        // Check that supplier was updated
        var getSupplierResponse = await _fixture.SupplierServiceClient.GetAsync($"/api/suppliers/{supplierId}");
        var updatedSupplier = await getSupplierResponse.Content.ReadFromJsonAsync<dynamic>();
        ((string)updatedSupplier!.name).Should().Be("Updated Consistency Supplier");

        // Verify inventory items still reference the supplier correctly
        foreach (var productId in createdProductIds)
        {
            var inventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
            inventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var inventory = await inventoryResponse.Content.ReadFromJsonAsync<dynamic>();
            ((int)inventory!.supplierId).Should().Be((int)supplierId);
        }

        // Test supplier deactivation
        var deactivateDto = new
        {
            Name = "Updated Consistency Supplier",
            ContactPerson = "Updated Manager",
            Email = "updated@supplier.com",
            Phone = "+1-555-UPDT",
            Address = "Updated Address 789",
            City = "Updated City",
            State = "UP",
            PostalCode = "98765",
            Country = "USA",
            IsActive = false // Deactivate supplier
        };

        var deactivateResponse = await _fixture.SupplierServiceClient.PutAsJsonAsync($"/api/suppliers/{supplierId}", deactivateDto);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify supplier is deactivated
        var deactivatedSupplierResponse = await _fixture.SupplierServiceClient.GetAsync($"/api/suppliers/{supplierId}");
        var deactivatedSupplier = await deactivatedSupplierResponse.Content.ReadFromJsonAsync<dynamic>();
        ((bool)deactivatedSupplier!.isActive).Should().BeFalse();
    }

    [Fact]
    public async Task InventoryAlertKafkaWorkflow_ShouldPublishAndConsumeCorrectly()
    {
        // This test specifically validates the Kafka messaging workflow:
        // 1. Create inventory item with low reorder level
        // 2. Create order that brings inventory below reorder level
        // 3. Verify alert is published to Kafka
        // 4. Verify alert message structure and content

        // Arrange - Create inventory item with low reorder level
        var productId = Guid.NewGuid();
        var inventoryDto = new
        {
            ProductId = productId,
            ProductName = "Kafka Alert Test Product",
            Quantity = 15,
            ReorderLevel = 12, // Set high reorder level to trigger alert easily
            UnitPrice = 45.00m,
            SupplierId = 1, // Assume seeded supplier
            Location = "Kafka Test Warehouse"
        };

        var inventoryResponse = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventoryDto);
        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(1000);

        // Act - Create order that will trigger low stock alert
        var orderDto = new
        {
            CustomerName = "Kafka Test Customer",
            CustomerEmail = "kafka@test.com",
            Items = new[]
            {
                new
                {
                    ProductId = productId,
                    ProductName = "Kafka Alert Test Product",
                    Quantity = 8, // This will bring inventory to 7, below reorder level of 12
                    UnitPrice = 45.00m
                }
            }
        };

        var orderResponse = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", orderDto);
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Wait for Kafka message processing
        await Task.Delay(5000);

        // Assert - Verify Kafka alert was published
        var messages = await KafkaTestUtilities.ConsumeMessagesAsync("inventory-alerts", 
            _fixture.KafkaBootstrapAddress, TimeSpan.FromSeconds(15), maxMessages: 10);

        messages.Should().NotBeEmpty("Low stock alert should be published to Kafka");

        var alertMessage = messages.FirstOrDefault(m => m.Contains(productId.ToString()));
        alertMessage.Should().NotBeNull("Should receive alert for the specific product");

        // Verify message structure
        var messageJson = JsonConvert.DeserializeObject<dynamic>(alertMessage!);
        messageJson.Should().NotBeNull();
        ((string)messageJson!.productId).Should().Be(productId.ToString());
        ((string)messageJson.alertType).Should().Be("LowStock");
        ((int)messageJson.currentQuantity).Should().Be(7);
        ((int)messageJson.reorderLevel).Should().Be(12);
    }

    [Fact]
    public async Task MultiServiceErrorHandling_ShouldMaintainConsistency()
    {
        // This test validates error handling and consistency across services:
        // 1. Attempt to create order for non-existent product
        // 2. Attempt to create order with insufficient inventory
        // 3. Verify services handle errors gracefully without data corruption

        // Arrange
        var nonExistentProductId = Guid.NewGuid();
        
        // Act & Assert - Test order creation with non-existent product
        var invalidOrderDto = new
        {
            CustomerName = "Error Test Customer",
            CustomerEmail = "error@test.com",
            Items = new[]
            {
                new
                {
                    ProductId = nonExistentProductId,
                    ProductName = "Non-existent Product",
                    Quantity = 1,
                    UnitPrice = 10.00m
                }
            }
        };

        var invalidOrderResponse = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", invalidOrderDto);
        
        // Should either return BadRequest or handle gracefully
        invalidOrderResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);

        // Create valid inventory item for insufficient stock test
        var productId = Guid.NewGuid();
        var limitedInventoryDto = new
        {
            ProductId = productId,
            ProductName = "Limited Stock Product",
            Quantity = 5, // Only 5 in stock
            ReorderLevel = 2,
            UnitPrice = 25.00m,
            SupplierId = 1,
            Location = "Limited Warehouse"
        };

        var inventoryResponse = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", limitedInventoryDto);
        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(1000);

        // Attempt to order more than available
        var excessiveOrderDto = new
        {
            CustomerName = "Excessive Order Customer",
            CustomerEmail = "excessive@test.com",
            Items = new[]
            {
                new
                {
                    ProductId = productId,
                    ProductName = "Limited Stock Product",
                    Quantity = 10, // Requesting more than available (5)
                    UnitPrice = 25.00m
                }
            }
        };

        var excessiveOrderResponse = await _fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", excessiveOrderDto);
        
        // Should handle insufficient stock gracefully
        excessiveOrderResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);

        // Verify inventory quantity remains unchanged after failed order
        var checkInventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        checkInventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inventory = await checkInventoryResponse.Content.ReadFromJsonAsync<dynamic>();
        ((int)inventory!.quantity).Should().Be(5, "Inventory should remain unchanged after failed order");
    }

    [Fact]
    public async Task ConcurrentOrderProcessing_ShouldMaintainInventoryConsistency()
    {
        // This test validates that concurrent orders are handled correctly:
        // 1. Create inventory item with known quantity
        // 2. Create multiple concurrent orders
        // 3. Verify final inventory quantity is correct

        // Arrange - Create inventory item
        var productId = Guid.NewGuid();
        var inventoryDto = new
        {
            ProductId = productId,
            ProductName = "Concurrent Test Product",
            Quantity = 100,
            ReorderLevel = 20,
            UnitPrice = 15.00m,
            SupplierId = 1,
            Location = "Concurrent Warehouse"
        };

        var inventoryResponse = await _fixture.InventoryServiceClient.PostAsJsonAsync("/api/inventory", inventoryDto);
        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(1000);

        // Act - Create multiple concurrent orders
        var orderTasks = new List<Task<HttpResponseMessage>>();
        const int numberOfOrders = 5;
        const int quantityPerOrder = 10;

        for (int i = 0; i < numberOfOrders; i++)
        {
            var orderDto = new
            {
                CustomerName = $"Concurrent Customer {i}",
                CustomerEmail = $"concurrent{i}@test.com",
                Items = new[]
                {
                    new
                    {
                        ProductId = productId,
                        ProductName = "Concurrent Test Product",
                        Quantity = quantityPerOrder,
                        UnitPrice = 15.00m
                    }
                }
            };

            orderTasks.Add(_fixture.OrderServiceClient.PostAsJsonAsync("/api/orders", orderDto));
        }

        var responses = await Task.WhenAll(orderTasks);

        // Assert - Verify all orders were processed
        var successfulOrders = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        successfulOrders.Should().BeGreaterThan(0, "At least some orders should be successful");

        // Wait for all order processing to complete
        await Task.Delay(3000);

        // Verify final inventory quantity
        var finalInventoryResponse = await _fixture.InventoryServiceClient.GetAsync($"/api/inventory/{productId}");
        finalInventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalInventory = await finalInventoryResponse.Content.ReadFromJsonAsync<dynamic>();
        
        var expectedQuantity = 100 - (successfulOrders * quantityPerOrder);
        ((int)finalInventory!.quantity).Should().Be(expectedQuantity, 
            $"Final quantity should reflect all successful orders. Expected: {expectedQuantity}");
    }
}
