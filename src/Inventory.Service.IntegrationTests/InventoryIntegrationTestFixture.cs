using Confluent.Kafka;
using DotNet.Testcontainers.Builders;
using Inventory.Service.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.Kafka;
using Testcontainers.Oracle;
using Xunit;

namespace Inventory.Service.IntegrationTests;

public class InventoryIntegrationTestFixture : IAsyncLifetime
{
    private readonly OracleContainer _oracleContainer;
    private readonly KafkaContainer _kafkaContainer;
    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Client { get; private set; } = null!;
    public string OracleConnectionString => _oracleContainer.GetConnectionString();
    public string KafkaBootstrapServers => _kafkaContainer.GetBootstrapAddress();

    public InventoryIntegrationTestFixture()
    {
        // Configure Oracle container
        _oracleContainer = new OracleBuilder()
            .WithImage("gvenzl/oracle-xe:21-slim-faststart")
            .WithEnvironment("ORACLE_PASSWORD", "Oracle123!")
            .WithEnvironment("ORACLE_DATABASE", "TESTDB")
            .Build();

        // Configure Kafka container
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.1")
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "true")
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start containers in parallel
        var oracleStartTask = _oracleContainer.StartAsync();
        var kafkaStartTask = _kafkaContainer.StartAsync();

        await Task.WhenAll(oracleStartTask, kafkaStartTask);

        // Wait a bit for Oracle to be fully ready
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Create the web application factory
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registration
                    services.RemoveAll<DbContextOptions<InventoryDbContext>>();
                    services.RemoveAll<InventoryDbContext>();

                    // Add test database context with Oracle connection string
                    services.AddDbContext<InventoryDbContext>(options =>
                    {
                        options.UseOracle(OracleConnectionString);
                    });

                    // Override Kafka configuration
                    services.Configure<ConnectionStrings>(config =>
                    {
                        config.DefaultConnection = OracleConnectionString;
                        config.Kafka = KafkaBootstrapServers;
                    });
                });

                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = OracleConnectionString,
                        ["ConnectionStrings:Kafka"] = KafkaBootstrapServers,
                        ["Kafka:BootstrapServers"] = KafkaBootstrapServers,
                        ["Kafka:Topics:LowStockAlerts"] = "inventory.alerts",
                        ["Kafka:Topics:InventoryUpdated"] = "inventory.updated",
                        ["JWT:Key"] = "MyVeryLongAndSecureKeyForJWTTokenGeneration123456",
                        ["JWT:Issuer"] = "ERPInventoryModule",
                        ["JWT:Audience"] = "ERPInventoryModule"
                    });
                });
            });

        Client = _factory.CreateClient();

        // Initialize the database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        
        try
        {
            await context.Database.EnsureCreatedAsync();
            
            // Initialize with some basic data if needed
            if (!await context.InventoryItems.AnyAsync())
            {
                await DbInitializer.InitializeAsync(context);
            }
        }
        catch (Exception ex)
        {
            // Log the exception but don't fail the test setup
            Console.WriteLine($"Database initialization warning: {ex.Message}");
        }
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();

        // Stop containers
        var oracleStopTask = _oracleContainer.DisposeAsync().AsTask();
        var kafkaStopTask = _kafkaContainer.DisposeAsync().AsTask();

        await Task.WhenAll(oracleStopTask, kafkaStopTask);
    }

    public IConsumer<string, string> CreateKafkaConsumer(string groupId)
    {
        var config = new ConsumerConfig
        {
            GroupId = groupId,
            BootstrapServers = KafkaBootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            SessionTimeoutMs = 6000,
            HeartbeatIntervalMs = 2000
        };

        return new ConsumerBuilder<string, string>(config).Build();
    }

    public IProducer<string, string> CreateKafkaProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = KafkaBootstrapServers,
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        return new ProducerBuilder<string, string>(config).Build();
    }
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = string.Empty;
    public string Kafka { get; set; } = string.Empty;
}
