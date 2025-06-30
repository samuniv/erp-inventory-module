using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using Shared.Resilience;
using Testcontainers.PostgreSql;

namespace Shared.Resilience.IntegrationTests;

/// <summary>
/// Integration tests to verify database resilience policies work correctly under failure scenarios
/// </summary>
public class DatabaseResilienceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private ServiceProvider _serviceProvider;
    private IAsyncPolicy _databasePolicy;

    public DatabaseResilienceTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("test_resilience")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // Setup DI container with resilience policies
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddResiliencePolicies();
        
        _serviceProvider = services.BuildServiceProvider();
        _databasePolicy = _serviceProvider.GetRequiredKeyedService<IAsyncPolicy>("DatabasePolicy");
    }

    [Fact]
    public async Task Database_Retry_Policy_Should_Retry_On_Transient_Database_Errors()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        var retryCount = 0;

        // Simulate a database operation that fails transiently
        var databaseOperation = async () =>
        {
            retryCount++;
            
            // Simulate transient failure for first two attempts
            if (retryCount <= 2)
            {
                throw new Npgsql.NpgsqlException("Connection timeout");
            }
            
            // Succeed on third attempt
            using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return "Success";
        };

        // Act
        var result = await _databasePolicy.ExecuteAsync(async () => await databaseOperation());

        // Assert
        result.Should().Be("Success");
        retryCount.Should().Be(3, "Should retry twice before succeeding on third attempt");
    }

    [Fact]
    public async Task Database_Retry_Policy_Should_Not_Retry_On_Non_Transient_Errors()
    {
        // Arrange
        var retryCount = 0;

        // Simulate a database operation that fails with non-transient error
        var databaseOperation = async () =>
        {
            retryCount++;
            
            // Simulate non-transient failure (authentication error)
            throw new Npgsql.NpgsqlException("Authentication failed");
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Npgsql.NpgsqlException>(
            async () => await _databasePolicy.ExecuteAsync(async () => await databaseOperation()));

        exception.Message.Should().Contain("Authentication failed");
        retryCount.Should().Be(1, "Should not retry non-transient errors");
    }

    [Fact]
    public async Task Database_Policy_Should_Handle_Deadlock_Scenarios()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        var deadlockCount = 0;

        // Simulate deadlock scenario that resolves after retry
        var databaseOperation = async () =>
        {
            deadlockCount++;
            
            if (deadlockCount == 1)
            {
                // Simulate deadlock on first attempt
                throw new Npgsql.NpgsqlException("deadlock detected");
            }
            
            // Succeed on retry
            using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync();
            
            return result?.ToString() ?? "null";
        };

        // Act
        var result = await _databasePolicy.ExecuteAsync(async () => await databaseOperation());

        // Assert
        result.Should().Be("1");
        deadlockCount.Should().Be(2, "Should retry once after deadlock");
    }

    [Fact]
    public async Task Database_Policy_Should_Handle_Transaction_Rollback()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        var operationCount = 0;

        // Create test table
        using (var setupConnection = new Npgsql.NpgsqlConnection(connectionString))
        {
            await setupConnection.OpenAsync();
            using var setupCommand = setupConnection.CreateCommand();
            setupCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS test_resilience_table (
                    id SERIAL PRIMARY KEY,
                    name TEXT NOT NULL
                )";
            await setupCommand.ExecuteNonQueryAsync();
        }

        // Simulate transaction that fails and needs retry
        var transactionOperation = async () =>
        {
            operationCount++;
            
            using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO test_resilience_table (name) VALUES (@name)";
                command.Parameters.AddWithValue("name", $"Test-{operationCount}");
                
                await command.ExecuteNonQueryAsync();
                
                // Simulate failure on first attempt that requires rollback
                if (operationCount == 1)
                {
                    throw new InvalidOperationException("Simulated transaction failure");
                }
                
                await transaction.CommitAsync();
                return "Transaction completed";
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync();
                throw;
            }
        };

        // Act
        var result = await _databasePolicy.ExecuteAsync(async () => await transactionOperation());

        // Assert
        result.Should().Be("Transaction completed");
        operationCount.Should().Be(2, "Should retry once after transaction failure");

        // Verify only the successful transaction was committed
        using var verifyConnection = new Npgsql.NpgsqlConnection(connectionString);
        await verifyConnection.OpenAsync();
        using var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT COUNT(*) FROM test_resilience_table";
        var count = await verifyCommand.ExecuteScalarAsync();
        count.Should().Be(1L, "Only the successful transaction should be committed");
    }

    [Fact]
    public async Task Database_Policy_Should_Respect_Maximum_Retry_Attempts()
    {
        // Arrange
        var retryCount = 0;
        const int maxExpectedRetries = 3; // Based on our policy configuration

        // Simulate persistent database failure
        var failingOperation = async () =>
        {
            retryCount++;
            throw new Npgsql.NpgsqlException("Persistent connection failure");
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Npgsql.NpgsqlException>(
            async () => await _databasePolicy.ExecuteAsync(async () => await failingOperation()));

        exception.Message.Should().Contain("Persistent connection failure");
        retryCount.Should().BeLessThanOrEqualTo(maxExpectedRetries + 1);
    }

    [Fact]
    public async Task Database_Policy_Should_Use_Exponential_Backoff()
    {
        // Arrange
        var retryCount = 0;
        var retryTimestamps = new List<DateTime>();

        // Simulate operation that tracks retry timing
        var timedOperation = async () =>
        {
            retryTimestamps.Add(DateTime.UtcNow);
            retryCount++;
            
            if (retryCount <= 2)
            {
                throw new Npgsql.NpgsqlException("Timeout for retry timing test");
            }
            
            return "Timing test completed";
        };

        // Act
        var result = await _databasePolicy.ExecuteAsync(async () => await timedOperation());

        // Assert
        result.Should().Be("Timing test completed");
        retryTimestamps.Should().HaveCount(3, "Should have timestamps for all attempts");

        if (retryTimestamps.Count >= 2)
        {
            var firstRetryDelay = retryTimestamps[1] - retryTimestamps[0];
            firstRetryDelay.Should().BeGreaterThan(TimeSpan.FromMilliseconds(500), 
                "First retry should have exponential backoff delay");
        }

        if (retryTimestamps.Count >= 3)
        {
            var secondRetryDelay = retryTimestamps[2] - retryTimestamps[1];
            secondRetryDelay.Should().BeGreaterThan(TimeSpan.FromSeconds(1), 
                "Second retry should have longer exponential backoff delay");
        }
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        _serviceProvider?.Dispose();
    }
}
