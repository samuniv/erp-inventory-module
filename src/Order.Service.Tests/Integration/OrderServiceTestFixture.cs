using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Order.Service.Data;
using Order.Service.Services;
using Testcontainers.PostgreSql;
using Testcontainers.Kafka;
using Moq;
using Xunit;

namespace Order.Service.Tests.Integration;

/// <summary>
/// Test fixture for Order Service integration tests with Testcontainers
/// </summary>
public class OrderServiceTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("test_erp_orders")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .WithPortBinding(5433, 5432)
        .Build();

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.1")
        .WithPortBinding(9093, 9092)
        .Build();

    public string PostgresConnectionString => _postgresContainer.GetConnectionString();
    public string KafkaBootstrapServers => _kafkaContainer.GetBootstrapAddress();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<OrderDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add test database context
            services.AddDbContext<OrderDbContext>(options =>
                options.UseNpgsql(PostgresConnectionString));

            // Mock the event publisher for testing
            var mockEventPublisher = new Mock<IEventPublisher>();
            mockEventPublisher
                .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Events.EventPublishResult.Success("test-topic", 0, 0));

            services.AddScoped(_ => mockEventPublisher.Object);

            // Override Kafka configuration for testing
            services.Configure<Events.KafkaOptions>(options =>
            {
                options.BootstrapServers = KafkaBootstrapServers;
                options.GroupId = "test-order-service";
            });
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _kafkaContainer.StartAsync();

        // Initialize the database
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
        await _kafkaContainer.StopAsync();
        await base.DisposeAsync();
    }
}
