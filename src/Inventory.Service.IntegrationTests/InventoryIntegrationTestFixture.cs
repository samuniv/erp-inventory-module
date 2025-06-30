using Confluent.Kafka;
using Inventory.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.TestInfrastructure.Base;
using Xunit;

namespace Inventory.Service.IntegrationTests;

/// <summary>
/// Test fixture for Inventory Service integration tests using shared test infrastructure
/// </summary>
public class InventoryIntegrationTestFixture : ServiceIntegrationTestBase<Program, InventoryDbContext>
{
    public InventoryIntegrationTestFixture() : base(usePostgreSql: false, useOracle: true, useKafka: true)
    {
    }

    protected override async Task SeedTestDataAsync(InventoryDbContext context)
    {
        // Initialize with some basic data if needed
        if (!await context.InventoryItems.AnyAsync())
        {
            await DbInitializer.InitializeAsync(context);
        }
    }

    /// <summary>
    /// Create a consumer for testing Kafka messages
    /// </summary>
    public IConsumer<string, string> CreateKafkaConsumer(string groupId)
    {
        return Infrastructure.CreateKafkaConsumer(groupId);
    }

    /// <summary>
    /// Create a producer for testing Kafka messages
    /// </summary>
    public IProducer<string, string> CreateKafkaProducer()
    {
        return Infrastructure.CreateKafkaProducer();
    }
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = string.Empty;
    public string Kafka { get; set; } = string.Empty;
}
