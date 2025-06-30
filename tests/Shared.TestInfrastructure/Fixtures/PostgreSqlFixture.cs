using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace Shared.TestInfrastructure.Fixtures;

/// <summary>
/// Base fixture for PostgreSQL database integration tests using Testcontainers
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private readonly ILogger<PostgreSqlFixture> _logger;

    public string ConnectionString => _container.GetConnectionString();

    public PostgreSqlFixture()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<PostgreSqlFixture>();

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("test_erp_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithPortBinding(0, 5432) // Random port to avoid conflicts
            .Build();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Starting PostgreSQL container...");
            await _container.StartAsync();
            _logger.LogInformation("PostgreSQL container started successfully. Connection string: {ConnectionString}", ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start PostgreSQL container");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _logger.LogInformation("Stopping PostgreSQL container...");
            await _container.DisposeAsync();
            _logger.LogInformation("PostgreSQL container stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing PostgreSQL container");
        }
    }

    /// <summary>
    /// Create a consumer for testing Kafka messages
    /// </summary>
    public IConsumer<string, string> CreateKafkaConsumer(string groupId)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092", // Default Kafka port
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        return new ConsumerBuilder<string, string>(config).Build();
    }

    /// <summary>
    /// Create a producer for testing Kafka messages
    /// </summary>
    public IProducer<string, string> CreateKafkaProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = "localhost:9092", // Default Kafka port
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        return new ProducerBuilder<string, string>(config).Build();
    }
}
