using Confluent.Kafka;
using Inventory.Service.Data;
using Inventory.Service.DTOs;
using Inventory.Service.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using System.Text.Json;
using Testcontainers.Kafka;
using Testcontainers.Oracle;
using Xunit;

namespace Inventory.Service.IntegrationTests;

[Collection("Oracle Integration Tests")]
public class InventoryServiceIntegrationTests : IClassFixture<InventoryIntegrationTestFixture>
{
    private readonly InventoryIntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public InventoryServiceIntegrationTests(InventoryIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.Client;
    }

    [Fact]
    public async Task CreateInventoryItem_WithLowStock_ShouldPublishLowStockAlert()
    {
        // Arrange
        var createDto = new CreateInventoryItemDto
        {
            Name = "Test Widget",
            Description = "A test widget for integration testing",
            Sku = "TST-WIDGET-001",
            StockLevel = 5,
            ReorderThreshold = 10,
            UnitPrice = 29.99m,
            Unit = "pieces",
            Category = "Test Items",
            SupplierId = 1
        };

        // Start Kafka consumer to capture the alert
        var alertReceived = new TaskCompletionSource<LowStockAlertEvent>();
        var consumer = _fixture.CreateKafkaConsumer("test-group-create");

        var consumerTask = Task.Run(async () =>
        {
            try
            {
                consumer.Subscribe("inventory.alerts");
                while (!alertReceived.Task.IsCompleted)
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(5));
                    if (result != null)
                    {
                        var alert = JsonSerializer.Deserialize<LowStockAlertEvent>(result.Message.Value);
                        if (alert != null && alert.Sku == createDto.Sku)
                        {
                            alertReceived.SetResult(alert);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                alertReceived.SetException(ex);
            }
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/inventory", createDto);

        // Assert
        response.EnsureSuccessStatusCode();
        var createdItem = await response.Content.ReadFromJsonAsync<InventoryItemDto>();

        Assert.NotNull(createdItem);
        Assert.Equal(createDto.Name, createdItem.Name);
        Assert.Equal(createDto.StockLevel, createdItem.StockLevel);

        // Wait for Kafka message
        var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(alert);
        Assert.Equal(createdItem.Id, alert.ItemId);
        Assert.Equal(createDto.Name, alert.ItemName);
        Assert.Equal(createDto.Sku, alert.Sku);
        Assert.Equal(createDto.StockLevel, alert.CurrentStockLevel);
        Assert.Equal(createDto.ReorderThreshold, alert.LowStockThreshold);
        Assert.Equal(AlertSeverity.Info, alert.Severity);

        consumer.Close();
    }

    [Fact]
    public async Task UpdateInventoryItem_StockBelowThreshold_ShouldPublishLowStockAlert()
    {
        // Arrange - First create an item with normal stock
        var createDto = new CreateInventoryItemDto
        {
            Name = "Update Test Widget",
            Description = "A widget for testing updates",
            Sku = "TST-UPDATE-001",
            StockLevel = 20,
            ReorderThreshold = 10,
            UnitPrice = 19.99m,
            Unit = "pieces",
            Category = "Test Items",
            SupplierId = 1
        };

        var createResponse = await _client.PostAsJsonAsync("/api/inventory", createDto);
        createResponse.EnsureSuccessStatusCode();
        var createdItem = await createResponse.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Update to trigger low stock
        var updateDto = new UpdateInventoryItemDto
        {
            Name = createdItem!.Name,
            Description = createdItem.Description,
            StockLevel = 3, // Below threshold
            ReorderThreshold = createdItem.ReorderThreshold,
            UnitPrice = createdItem.UnitPrice,
            Unit = createdItem.Unit,
            Category = createdItem.Category,
            SupplierId = createdItem.SupplierId,
            IsActive = true
        };

        // Start Kafka consumer
        var alertReceived = new TaskCompletionSource<LowStockAlertEvent>();
        var consumer = _fixture.CreateKafkaConsumer("test-group-update");

        var consumerTask = Task.Run(async () =>
        {
            try
            {
                consumer.Subscribe("inventory.alerts");
                while (!alertReceived.Task.IsCompleted)
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(5));
                    if (result != null)
                    {
                        var alert = JsonSerializer.Deserialize<LowStockAlertEvent>(result.Message.Value);
                        if (alert != null && alert.ItemId == createdItem.Id)
                        {
                            alertReceived.SetResult(alert);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                alertReceived.SetException(ex);
            }
        });

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/inventory/{createdItem.Id}", updateDto);

        // Assert
        updateResponse.EnsureSuccessStatusCode();
        var updatedItem = await updateResponse.Content.ReadFromJsonAsync<InventoryItemDto>();

        Assert.NotNull(updatedItem);
        Assert.Equal(updateDto.StockLevel, updatedItem.StockLevel);

        // Wait for Kafka message
        var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(alert);
        Assert.Equal(createdItem.Id, alert.ItemId);
        Assert.Equal(updateDto.StockLevel, alert.CurrentStockLevel);
        Assert.Equal(AlertSeverity.Warning, alert.Severity); // Should be warning for very low stock

        consumer.Close();
    }

    [Fact]
    public async Task AdjustStock_ToZero_ShouldPublishCriticalAlert()
    {
        // Arrange - Create an item
        var createDto = new CreateInventoryItemDto
        {
            Name = "Critical Test Widget",
            Description = "A widget for testing critical alerts",
            Sku = "TST-CRITICAL-001",
            StockLevel = 15,
            ReorderThreshold = 5,
            UnitPrice = 39.99m,
            Unit = "pieces",
            Category = "Test Items",
            SupplierId = 1
        };

        var createResponse = await _client.PostAsJsonAsync("/api/inventory", createDto);
        var createdItem = await createResponse.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Start Kafka consumer
        var alertReceived = new TaskCompletionSource<LowStockAlertEvent>();
        var consumer = _fixture.CreateKafkaConsumer("test-group-critical");

        var consumerTask = Task.Run(async () =>
        {
            try
            {
                consumer.Subscribe("inventory.alerts");
                while (!alertReceived.Task.IsCompleted)
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(5));
                    if (result != null)
                    {
                        var alert = JsonSerializer.Deserialize<LowStockAlertEvent>(result.Message.Value);
                        if (alert != null && alert.ItemId == createdItem!.Id && alert.CurrentStockLevel == 0)
                        {
                            alertReceived.SetResult(alert);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                alertReceived.SetException(ex);
            }
        });

        // Act - Adjust stock to zero
        var adjustRequest = new { Adjustment = -15, Reason = "Integration test - stock depletion" };
        var adjustResponse = await _client.PostAsJsonAsync($"/api/inventory/{createdItem!.Id}/adjust", adjustRequest);

        // Assert
        adjustResponse.EnsureSuccessStatusCode();
        var result = await adjustResponse.Content.ReadFromJsonAsync<dynamic>();

        // Wait for Kafka message
        var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(alert);
        Assert.Equal(createdItem.Id, alert.ItemId);
        Assert.Equal(0, alert.CurrentStockLevel);
        Assert.Equal(AlertSeverity.Critical, alert.Severity); // Should be critical for zero stock

        consumer.Close();
    }

    [Fact]
    public async Task GetLowStockItems_ShouldReturnItemsBelowThreshold()
    {
        // Arrange - Create items with different stock levels
        var items = new[]
        {
            new CreateInventoryItemDto
            {
                Name = "Low Stock Item 1",
                Sku = "LOW-STOCK-001",
                StockLevel = 2,
                ReorderThreshold = 10,
                UnitPrice = 10.00m,
                Unit = "pieces",
                Category = "Test Items",
                SupplierId = 1
            },
            new CreateInventoryItemDto
            {
                Name = "Normal Stock Item",
                Sku = "NORMAL-STOCK-001",
                StockLevel = 50,
                ReorderThreshold = 10,
                UnitPrice = 10.00m,
                Unit = "pieces",
                Category = "Test Items",
                SupplierId = 1
            },
            new CreateInventoryItemDto
            {
                Name = "Low Stock Item 2",
                Sku = "LOW-STOCK-002",
                StockLevel = 5,
                ReorderThreshold = 8,
                UnitPrice = 10.00m,
                Unit = "pieces",
                Category = "Test Items",
                SupplierId = 1
            }
        };

        // Create all items
        foreach (var item in items)
        {
            await _client.PostAsJsonAsync("/api/inventory", item);
        }

        // Act
        var response = await _client.GetAsync("/api/inventory/low-stock");

        // Assert
        response.EnsureSuccessStatusCode();
        var lowStockItems = await response.Content.ReadFromJsonAsync<List<InventoryItemListDto>>();

        Assert.NotNull(lowStockItems);
        Assert.True(lowStockItems.Count >= 2); // At least our 2 low stock items

        var lowStockSkus = lowStockItems.Select(i => i.Sku).ToList();
        Assert.Contains("LOW-STOCK-001", lowStockSkus);
        Assert.Contains("LOW-STOCK-002", lowStockSkus);
        Assert.DoesNotContain("NORMAL-STOCK-001", lowStockSkus);
    }
}
