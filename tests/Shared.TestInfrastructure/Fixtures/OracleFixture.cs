using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Testcontainers.Oracle;
using Xunit;

namespace Shared.TestInfrastructure.Fixtures;

/// <summary>
/// Base fixture for Oracle database integration tests using Testcontainers
/// </summary>
public class OracleFixture : IAsyncLifetime
{
    private readonly OracleContainer _container;
    private readonly ILogger<OracleFixture> _logger;

    public string ConnectionString => _container.GetConnectionString();

    public OracleFixture()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<OracleFixture>();

        _container = new OracleBuilder()
            .WithImage("gvenzl/oracle-xe:21-slim-faststart")
            .WithEnvironment("ORACLE_PASSWORD", "Oracle123!")
            .WithEnvironment("ORACLE_DATABASE", "TESTDB")
            .WithPortBinding(0, 1521) // Random port to avoid conflicts
            .Build();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Starting Oracle container...");
            await _container.StartAsync();

            // Oracle needs extra time to fully initialize
            _logger.LogInformation("Waiting for Oracle to fully initialize...");
            await Task.Delay(TimeSpan.FromSeconds(30));

            _logger.LogInformation("Oracle container started successfully. Connection string: {ConnectionString}", ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Oracle container");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _logger.LogInformation("Stopping Oracle container...");
            await _container.DisposeAsync();
            _logger.LogInformation("Oracle container stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing Oracle container");
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
