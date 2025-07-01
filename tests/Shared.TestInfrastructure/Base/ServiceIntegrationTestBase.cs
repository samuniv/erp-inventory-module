using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.TestInfrastructure.Fixtures;
using System.Collections.Generic;
using Xunit;

namespace Shared.TestInfrastructure.Base;

/// <summary>
/// Base class for service integration tests that provides common infrastructure setup
/// </summary>
/// <typeparam name="TStartup">The startup class of the service being tested</typeparam>
/// <typeparam name="TDbContext">The DbContext type used by the service</typeparam>
public abstract class ServiceIntegrationTestBase<TStartup, TDbContext> : WebApplicationFactory<TStartup>, IAsyncLifetime
    where TStartup : class
    where TDbContext : DbContext
{
    protected IntegrationTestFixture Infrastructure { get; }
    protected ILogger Logger { get; }

    public HttpClient Client { get; private set; } = null!;

    protected ServiceIntegrationTestBase(bool usePostgreSql = false, bool useOracle = false, bool useKafka = false)
    {
        Infrastructure = new IntegrationTestFixture(usePostgreSql, useOracle, useKafka);

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public virtual async Task InitializeAsync()
    {
        await Infrastructure.InitializeAsync();
        Client = CreateClient();

        // Initialize database if needed
        await InitializeDatabaseAsync();
    }

    public virtual new async Task DisposeAsync()
    {
        Client?.Dispose();
        await Infrastructure.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            ConfigureTestServices(services);
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testConfiguration = BuildTestConfiguration();
            config.AddInMemoryCollection(testConfiguration);
        });
    }

    /// <summary>
    /// Override this method to configure test-specific services
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Remove existing DbContext registration if using containers
        if (Infrastructure.PostgreSql != null || Infrastructure.Oracle != null)
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
        }

        // Add test database context
        if (Infrastructure.PostgreSql != null)
        {
            services.AddDbContext<TDbContext>(options =>
                options.UseNpgsql(Infrastructure.GetPostgreSqlConnectionString()));
        }
        else if (Infrastructure.Oracle != null)
        {
            services.AddDbContext<TDbContext>(options =>
                options.UseOracle(Infrastructure.GetOracleConnectionString()));
        }
    }

    /// <summary>
    /// Override this method to build test-specific configuration
    /// </summary>
    protected virtual Dictionary<string, string?> BuildTestConfiguration()
    {
        var configuration = new Dictionary<string, string?>();

        if (Infrastructure.PostgreSql != null)
        {
            configuration["ConnectionStrings:DefaultConnection"] = Infrastructure.GetPostgreSqlConnectionString();
        }
        else if (Infrastructure.Oracle != null)
        {
            configuration["ConnectionStrings:DefaultConnection"] = Infrastructure.GetOracleConnectionString();
        }

        if (Infrastructure.Kafka != null)
        {
            configuration["ConnectionStrings:Kafka"] = Infrastructure.GetKafkaBootstrapServers();
            configuration["Kafka:BootstrapServers"] = Infrastructure.GetKafkaBootstrapServers();
        }

        // Common test configuration
        configuration["JWT:Key"] = "MyVeryLongAndSecureKeyForJWTTokenGeneration123456";
        configuration["JWT:Issuer"] = "ERPInventoryModule";
        configuration["JWT:Audience"] = "ERPInventoryModule";

        return configuration;
    }

    /// <summary>
    /// Initialize the database for testing
    /// </summary>
    protected virtual async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        try
        {
            await context.Database.EnsureCreatedAsync();
            await SeedTestDataAsync(context);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize test database");
            throw;
        }
    }

    /// <summary>
    /// Override this method to seed test data
    /// </summary>
    protected virtual async Task SeedTestDataAsync(TDbContext context)
    {
        // Default implementation does nothing
        await Task.CompletedTask;
    }

    /// <summary>
    /// Create a new database scope for testing
    /// </summary>
    public IServiceScope CreateDatabaseScope()
    {
        return Services.CreateScope();
    }

    /// <summary>
    /// Get a database context from a scope
    /// </summary>
    public TDbContext GetDbContext(IServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<TDbContext>();
    }
}
