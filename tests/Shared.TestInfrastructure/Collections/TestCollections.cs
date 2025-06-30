using Xunit;

namespace Shared.TestInfrastructure.Collections;

/// <summary>
/// Collection fixture for PostgreSQL integration tests
/// Ensures that all tests in this collection share the same PostgreSQL container instance
/// </summary>
[CollectionDefinition(Name)]
public class PostgreSqlTestCollection : ICollectionFixture<Fixtures.PostgreSqlFixture>
{
    public const string Name = "PostgreSQL Integration Tests";
}

/// <summary>
/// Collection fixture for Oracle integration tests
/// Ensures that all tests in this collection share the same Oracle container instance
/// </summary>
[CollectionDefinition(Name)]
public class OracleTestCollection : ICollectionFixture<Fixtures.OracleFixture>
{
    public const string Name = "Oracle Integration Tests";
}

/// <summary>
/// Collection fixture for Kafka integration tests
/// Ensures that all tests in this collection share the same Kafka container instance
/// </summary>
[CollectionDefinition(Name)]
public class KafkaTestCollection : ICollectionFixture<Fixtures.KafkaFixture>
{
    public const string Name = "Kafka Integration Tests";
}

/// <summary>
/// Collection fixture for full integration tests with all infrastructure
/// Ensures that all tests in this collection share the same container instances
/// </summary>
[CollectionDefinition(Name)]
public class FullIntegrationTestCollection : ICollectionFixture<Fixtures.IntegrationTestFixture>
{
    public const string Name = "Full Integration Tests";
}

// Note: Use [Collection("PostgreSQL Integration Tests")] directly on test classes instead of custom attributes
// CollectionAttribute is sealed and cannot be inherited from
