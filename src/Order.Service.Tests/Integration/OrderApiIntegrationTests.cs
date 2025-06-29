using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Order.Service.Data;
using Order.Service.DTOs;
using Order.Service.Models;
using Xunit;

namespace Order.Service.Tests.Integration;

/// <summary>
/// Integration tests for Order API endpoints
/// </summary>
public class OrderApiIntegrationTests : IClassFixture<OrderServiceTestFixture>
{
    private readonly OrderServiceTestFixture _fixture;
    private readonly HttpClient _client;

    public OrderApiIntegrationTests(OrderServiceTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_WithValidData_ShouldReturnCreatedOrder()
    {
        // Arrange
        var createOrderRequest = new CreateOrderRequest
        {
            CustomerName = "John Doe",
            CustomerEmail = "john.doe@example.com",
            CustomerPhone = "+1234567890",
            ShippingAddress = "123 Main St, Anytown, ST, 12345, USA",
            Notes = "Test order for integration testing",
            OrderItems = new List<CreateOrderItemRequest>
            {
                new()
                {
                    InventoryItemId = 1,
                    ItemName = "Test Product",
                    ItemSku = "TEST-001",
                    ItemDescription = "Test product description",
                    Quantity = 2,
                    UnitPrice = 25.00m,
                    Category = "Electronics",
                    SupplierName = "Test Supplier"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", createOrderRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdOrder = await response.Content.ReadFromJsonAsync<OrderResponse>();
        createdOrder.Should().NotBeNull();
        createdOrder!.CustomerName.Should().Be("John Doe");
        createdOrder.CustomerEmail.Should().Be("john.doe@example.com");
        createdOrder.TotalAmount.Should().BeGreaterThan(0);
        createdOrder.OrderItems.Should().HaveCount(1);
        createdOrder.OrderItems[0].ItemName.Should().Be("Test Product");
        createdOrder.OrderNumber.Should().StartWith("ORD-");
    }

    [Fact]
    public async Task CreateOrder_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidRequest = new CreateOrderRequest
        {
            CustomerName = "", // Invalid: empty name
            CustomerEmail = "invalid-email", // Invalid: bad email format
            OrderItems = new List<CreateOrderItemRequest>() // Invalid: no items
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrder_WithValidId_ShouldReturnOrder()
    {
        // Arrange - First create an order
        var createRequest = CreateValidOrderRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Act
        var response = await _client.GetAsync($"/api/orders/{createdOrder!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var retrievedOrder = await response.Content.ReadFromJsonAsync<OrderResponse>();
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.Id.Should().Be(createdOrder.Id);
        retrievedOrder.CustomerName.Should().Be(createdOrder.CustomerName);
    }

    [Fact]
    public async Task GetOrder_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrders_ShouldReturnPaginatedList()
    {
        // Arrange - Create multiple orders
        for (int i = 0; i < 3; i++)
        {
            var request = CreateValidOrderRequest($"Customer {i}");
            await _client.PostAsJsonAsync("/api/orders", request);
        }

        // Act
        var response = await _client.GetAsync("/api/orders?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.Should().NotBeNull();
        orders!.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithValidData_ShouldUpdateStatus()
    {
        // Arrange - Create an order first
        var createRequest = CreateValidOrderRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var updateRequest = new { Status = "Confirmed" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/orders/{createdOrder!.Id}/status", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updatedOrder = await response.Content.ReadFromJsonAsync<OrderResponse>();
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task CancelOrder_WithValidId_ShouldCancelOrder()
    {
        // Arrange - Create an order first
        var createRequest = CreateValidOrderRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var cancelRequest = new { Reason = "Customer requested cancellation" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/orders/{createdOrder!.Id}/cancel", cancelRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the order was cancelled
        var getResponse = await _client.GetAsync($"/api/orders/{createdOrder.Id}");
        var cancelledOrder = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();
        cancelledOrder!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task OrderWorkflow_FullLifecycle_ShouldWorkCorrectly()
    {
        // 1. Create order
        var createRequest = CreateValidOrderRequest("Workflow Customer");
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        order!.Status.Should().Be("Pending");

        // 2. Confirm order
        var confirmRequest = new { Status = "Confirmed" };
        var confirmResponse = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", confirmRequest);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Process order
        var processRequest = new { Status = "Processing" };
        var processResponse = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", processRequest);
        processResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Ship order
        var shipRequest = new { Status = "Shipped" };
        var shipResponse = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", shipRequest);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Deliver order
        var deliverRequest = new { Status = "Delivered" };
        var deliverResponse = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", deliverRequest);
        deliverResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify final state
        var finalResponse = await _client.GetAsync($"/api/orders/{order.Id}");
        var finalOrder = await finalResponse.Content.ReadFromJsonAsync<OrderResponse>();
        finalOrder!.Status.Should().Be("Delivered");
        finalOrder.DeliveredDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Database_OrderPersistence_ShouldWorkCorrectly()
    {
        // Arrange
        var createRequest = CreateValidOrderRequest("Persistence Test Customer");

        // Act - Create order
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        // Assert - Verify in database
        using var scope = _fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        
        var dbOrder = await context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == createdOrder!.Id);

        dbOrder.Should().NotBeNull();
        dbOrder!.CustomerName.Should().Be("Persistence Test Customer");
        dbOrder.OrderItems.Should().HaveCount(1);
        dbOrder.TotalAmount.Should().BeGreaterThan(0);
        dbOrder.OrderNumber.Should().StartWith("ORD-");
    }

    private static CreateOrderRequest CreateValidOrderRequest(string customerName = "Test Customer")
    {
        return new CreateOrderRequest
        {
            CustomerName = customerName,
            CustomerEmail = $"{customerName.Replace(" ", "").ToLower()}@example.com",
            CustomerPhone = "+1234567890",
            ShippingAddress = "123 Test Street, Test City, TS, 12345, USA",
            Notes = "Automated test order",
            OrderItems = new List<CreateOrderItemRequest>
            {
                new()
                {
                    InventoryItemId = 1,
                    ItemName = "Test Widget",
                    ItemSku = "TEST-WIDGET-001",
                    ItemDescription = "A test widget for automated testing",
                    Quantity = 1,
                    UnitPrice = 19.99m,
                    Category = "Test Category",
                    SupplierName = "Test Supplier Co."
                }
            }
        };
    }
}
