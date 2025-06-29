using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supplier.Service.Data;
using Testcontainers.Oracle;

namespace Supplier.Service.IntegrationTests.Fixtures;

public class SupplierIntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly OracleContainer _oracleContainer = new OracleBuilder()
        .WithImage("gvenzl/oracle-xe:21-slim-faststart")
        .WithPassword("oracle")
        .WithPortBinding(0, 1521)
        .Build();

    public string ConnectionString => _oracleContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<SupplierDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add a database context using the test container
            services.AddDbContext<SupplierDbContext>(options =>
            {
                options.UseOracle(ConnectionString);
            });

            // Build the service provider and ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<SupplierDbContext>();
            var logger = scopedServices.GetRequiredService<ILogger<SupplierIntegrationTestFixture>>();

            try
            {
                db.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred creating the test database");
                throw;
            }
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _oracleContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _oracleContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public SupplierDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SupplierDbContext>()
            .UseOracle(ConnectionString)
            .Options;

        return new SupplierDbContext(options);
    }
}
