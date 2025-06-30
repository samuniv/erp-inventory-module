using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Shared.TestInfrastructure.Fixtures;

/// <summary>
/// Composite fixture that manages multiple containers for comprehensive integration testing
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly ILogger<IntegrationTestFixture> _logger;
    
    public PostgreSqlFixture? PostgreSql { get; private set; }
    public OracleFixture? Oracle { get; private set; }
    public KafkaFixture? Kafka { get; private set; }

    public IntegrationTestFixture(bool includePostgreSql = true, bool includeOracle = true, bool includeKafka = true)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<IntegrationTestFixture>();

        if (includePostgreSql)
        {
            PostgreSql = new PostgreSqlFixture();
        }

        if (includeOracle)
        {
            Oracle = new OracleFixture();
        }

        if (includeKafka)
        {
            Kafka = new KafkaFixture();
        }
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing integration test infrastructure...");

        var initializationTasks = new List<Task>();

        if (PostgreSql != null)
        {
            initializationTasks.Add(PostgreSql.InitializeAsync());
        }

        if (Oracle != null)
        {
            initializationTasks.Add(Oracle.InitializeAsync());
        }

        if (Kafka != null)
        {
            initializationTasks.Add(Kafka.InitializeAsync());
        }

        try
        {
            await Task.WhenAll(initializationTasks);
            _logger.LogInformation("Integration test infrastructure initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize integration test infrastructure");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _logger.LogInformation("Disposing integration test infrastructure...");

        var disposalTasks = new List<Task>();

        if (PostgreSql != null)
        {
            disposalTasks.Add(PostgreSql.DisposeAsync());
        }

        if (Oracle != null)
        {
            disposalTasks.Add(Oracle.DisposeAsync());
        }

        if (Kafka != null)
        {
            disposalTasks.Add(Kafka.DisposeAsync());
        }

        try
        {
            await Task.WhenAll(disposalTasks);
            _logger.LogInformation("Integration test infrastructure disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing integration test infrastructure");
        }
    }

    /// <summary>
    /// Get PostgreSQL connection string
    /// </summary>
    public string GetPostgreSqlConnectionString()
    {
        if (PostgreSql == null)
            throw new InvalidOperationException("PostgreSQL fixture is not enabled");
        
        return PostgreSql.ConnectionString;
    }

    /// <summary>
    /// Get Oracle connection string
    /// </summary>
    public string GetOracleConnectionString()
    {
        if (Oracle == null)
            throw new InvalidOperationException("Oracle fixture is not enabled");
        
        return Oracle.ConnectionString;
    }

    /// <summary>
    /// Get Kafka bootstrap servers
    /// </summary>
    public string GetKafkaBootstrapServers()
    {
        if (Kafka == null)
            throw new InvalidOperationException("Kafka fixture is not enabled");
        
        return Kafka.BootstrapServers;
    }

    /// <summary>
    /// Create a Kafka consumer from the appropriate fixture
    /// </summary>
    public IConsumer<string, string> CreateKafkaConsumer(string groupId)
    {
        if (Kafka == null)
            throw new InvalidOperationException("Kafka fixture is not enabled");
        
        return Kafka.CreateKafkaConsumer(groupId);
    }

    /// <summary>
    /// Create a Kafka producer from the appropriate fixture
    /// </summary>
    public IProducer<string, string> CreateKafkaProducer()
    {
        if (Kafka == null)
            throw new InvalidOperationException("Kafka fixture is not enabled");
        
        return Kafka.CreateKafkaProducer();
    }

    /// <summary>
    /// Create a topic for testing
    /// </summary>
    public async Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1)
    {
        if (Kafka == null)
            throw new InvalidOperationException("Kafka fixture is not enabled");
        
        await Kafka.CreateTopicAsync(topicName, partitions, replicationFactor);
    }

    /// <summary>
    /// Wait for a message on a topic
    /// </summary>
    public async Task<ConsumeResult<string, string>?> WaitForMessageAsync(
        string topic, 
        string consumerGroup, 
        TimeSpan timeout,
        Func<ConsumeResult<string, string>, bool>? predicate = null)
    {
        if (Kafka == null)
            throw new InvalidOperationException("Kafka fixture is not enabled");
        
        return await Kafka.WaitForMessageAsync(topic, consumerGroup, timeout, predicate);
    }
}
