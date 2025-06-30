using Microsoft.Extensions.DependencyInjection;
using Supplier.Service.Data;
using Shared.TestInfrastructure.Base;
using Xunit;

namespace Supplier.Service.IntegrationTests.Fixtures;

/// <summary>
/// Test fixture for Supplier Service integration tests using shared test infrastructure
/// </summary>
public class SupplierIntegrationTestFixture : ServiceIntegrationTestBase<Program, SupplierDbContext>
{
    public SupplierIntegrationTestFixture() : base(usePostgreSql: false, useOracle: true, useKafka: false)
    {
    }

    protected override async Task SeedTestDataAsync(SupplierDbContext context)
    {
        // Initialize with some basic data if needed for Supplier Service tests
        await Task.CompletedTask;
    }
}
