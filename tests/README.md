# Integration Testing with Testcontainers

This directory contains the integration testing infrastructure for the ERP Inventory Module, utilizing Testcontainers for containerized test dependencies.

## Overview

The integration testing setup provides:

- **Automated Infrastructure**: Database, messaging, and caching services via Docker containers
- **Testcontainers Integration**: .NET Testcontainers for PostgreSQL, Oracle, Kafka, and Redis
- **CI/CD Integration**: GitHub Actions workflows for automated testing
- **Local Development**: Scripts and Docker Compose for local testing

## Test Categories

### Database Tests

- **Target**: Repository patterns, Entity Framework migrations, data persistence
- **Infrastructure**: PostgreSQL and Oracle containers
- **Filter**: `FullyQualifiedName~IntegrationTests&Category!=EndToEnd&Category!=Messaging`

### Messaging Tests

- **Target**: Kafka producers, consumers, event handling
- **Infrastructure**: Kafka and Zookeeper containers
- **Filter**: `FullyQualifiedName~IntegrationTests&Category=Messaging`

### End-to-End Tests

- **Target**: Complete workflows across multiple services
- **Infrastructure**: All services and dependencies
- **Filter**: `FullyQualifiedName~IntegrationTests&Category=EndToEnd`

## Running Integration Tests

### Local Development

#### Using Scripts (Recommended)

**Linux/macOS:**

```bash
# Run all integration tests
./scripts/run-integration-tests.sh

# Run specific test suite
./scripts/run-integration-tests.sh --suite database
./scripts/run-integration-tests.sh --suite messaging
./scripts/run-integration-tests.sh --suite end-to-end

# Run with options
./scripts/run-integration-tests.sh --parallel --verbose --no-cleanup
```

**Windows PowerShell:**

```powershell
# Run all integration tests
.\scripts\run-integration-tests.ps1

# Run specific test suite
.\scripts\run-integration-tests.ps1 -Suite database
.\scripts\run-integration-tests.ps1 -Suite messaging
.\scripts\run-integration-tests.ps1 -Suite end-to-end

# Run with options
.\scripts\run-integration-tests.ps1 -Parallel -Verbose -NoCleanup
```

#### Using Docker Compose

```bash
# Start infrastructure services
docker-compose -f docker-compose.integration.yml up -d

# Run tests manually
dotnet test ERPInventoryModule.sln \
  --configuration Release \
  --filter "FullyQualifiedName~IntegrationTests" \
  --settings ./tests/integration.runsettings

# Cleanup
docker-compose -f docker-compose.integration.yml down -v
```

#### Using .NET CLI Directly

```bash
# Build solution
dotnet build ERPInventoryModule.sln --configuration Release

# Run integration tests
dotnet test ERPInventoryModule.sln \
  --configuration Release \
  --no-build \
  --filter "FullyQualifiedName~IntegrationTests" \
  --logger trx \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults \
  --settings ./tests/integration.runsettings
```

### GitHub Actions CI/CD

Integration tests run automatically on:

- **Pull Requests**: Limited integration tests for feedback
- **Scheduled**: Full nightly integration test suite
- **Manual Trigger**: On-demand execution with configurable test suites

#### Manual Workflow Dispatch

1. Go to **Actions** → **Integration Tests** in GitHub
2. Click **Run workflow**
3. Select test suite: `all`, `database-only`, `kafka-only`, `end-to-end`
4. Enable/disable parallel execution
5. Click **Run workflow**

## Configuration

### Test Settings

Integration test behavior is configured in `tests/integration.runsettings`:

```xml
<EnvironmentVariables>
  <TESTCONTAINERS_RYUK_DISABLED>true</TESTCONTAINERS_RYUK_DISABLED>
  <TESTCONTAINERS_CHECKS_DISABLE>true</TESTCONTAINERS_CHECKS_DISABLE>
  <DB_CONNECTION_TIMEOUT>60</DB_CONNECTION_TIMEOUT>
  <TEST_PARALLELIZATION>false</TEST_PARALLELIZATION>
</EnvironmentVariables>
```

### Environment Variables

Key environment variables for integration tests:

- `TESTCONTAINERS_RYUK_DISABLED`: Disable Ryuk container reaper (CI environments)
- `TESTCONTAINERS_CHECKS_DISABLE`: Skip pre-flight checks (CI environments)
- `DOCKER_HOST`: Docker daemon socket (usually `unix:///var/run/docker.sock`)
- `TESTCONTAINERS_HOST_OVERRIDE`: Override for container host resolution

### Service Configuration

#### PostgreSQL

- **Image**: `postgres:15-alpine`
- **Database**: `erp_test`
- **Port**: `5432`
- **Credentials**: `test_user` / `test_password`

#### Oracle

- **Image**: `container-registry.oracle.com/database/express:21.3.0-xe`
- **Port**: `1521`
- **Credentials**: `system` / `TestPassword123`

#### Kafka

- **Image**: `confluentinc/cp-kafka:latest`
- **Port**: `9092`
- **Zookeeper**: `confluentinc/cp-zookeeper:latest` on port `2181`

#### Redis

- **Image**: `redis:7-alpine`
- **Port**: `6379`

## Test Structure

### Test Projects

- `tests/Shared.Tests/`: Unit tests with some integration test utilities
- `src/Inventory.Service.IntegrationTests/`: Inventory service integration tests
- `tests/EndToEnd.Integration.Tests/`: Cross-service integration tests
- `Supplier.Service.IntegrationTests/`: Supplier service integration tests

### Test Fixtures

Shared test infrastructure in `tests/Shared.TestInfrastructure/`:

- `IntegrationTestFixture`: Composite fixture managing multiple containers
- `PostgreSqlFixture`: PostgreSQL database container management
- `OracleFixture`: Oracle database container management
- `KafkaFixture`: Kafka messaging container management

### Collection Definitions

Test collections ensure proper resource sharing:

```csharp
[Collection("PostgreSQL Integration Tests")]
public class InventoryRepositoryTests : IClassFixture<PostgreSqlFixture>

[Collection("Oracle Integration Tests")]
public class SupplierServiceTests : IClassFixture<OracleFixture>

[Collection("Kafka Integration Tests")]
public class EventProcessingTests : IClassFixture<KafkaFixture>
```

## Troubleshooting

### Common Issues

#### Docker Not Available

```bash
Error: Docker is not running
```

**Solution**: Start Docker Desktop or Docker daemon

#### Port Conflicts

```bash
Error: Port 5432 is already in use
```

**Solution**: Stop conflicting services or change ports in `docker-compose.integration.yml`

#### Container Startup Timeouts

```bash
Timeout waiting for container to be ready
```

**Solution**:

- Increase timeout values in test settings
- Check Docker resources (CPU/Memory)
- Review container logs: `docker logs <container_name>`

#### Testcontainers Permission Issues

```bash
Error: Cannot connect to Docker daemon
```

**Solution**:

- Add user to docker group: `sudo usermod -aG docker $USER`
- Use `sudo` for test execution (not recommended)
- Configure Docker daemon socket permissions

### Debug Mode

For debugging integration tests:

1. **Keep Containers Running**: Use `--no-cleanup` flag
2. **Enable Verbose Output**: Use `--verbose` flag
3. **Check Container Logs**: `docker-compose -f docker-compose.integration.yml logs`
4. **Connect to Containers**: `docker exec -it <container_name> bash`

### Performance Optimization

- **Parallel Execution**: Use `--parallel` flag for faster execution
- **Container Reuse**: Enable in development for faster test iterations
- **Resource Limits**: Configure Docker with adequate CPU/Memory
- **Image Caching**: Pre-pull images: `docker-compose pull`

## CI/CD Pipeline Details

### GitHub Actions Workflows

#### Main Workflow (`dotnet.yml`)

- **Trigger**: Push to main/develop, Pull requests
- **Scope**: Build, unit tests, basic integration tests
- **Duration**: ~10-15 minutes

#### Integration Tests Workflow (`integration-tests.yml`)

- **Trigger**: Manual dispatch, scheduled (nightly), pull requests
- **Scope**: Full integration test suite with matrix strategy
- **Duration**: ~30-45 minutes

### Matrix Strategy

Integration tests run in parallel across test groups:

```yaml
strategy:
  matrix:
    test-group:
      - database-tests
      - messaging-tests
      - end-to-end-tests
```

This provides:

- **Faster Feedback**: Parallel execution reduces total runtime
- **Isolation**: Test groups run independently
- **Granular Results**: Separate results per test category

### Artifact Collection

- **Test Results**: TRX files uploaded as artifacts
- **Coverage Reports**: Cobertura XML uploaded to Codecov
- **Logs**: Container logs available for debugging

## Best Practices

### Test Design

1. **Use Test Categories**: Properly categorize tests for filtering
2. **Manage Resources**: Use fixtures for expensive resource setup
3. **Clean State**: Ensure tests start with clean database state
4. **Timeout Handling**: Set appropriate timeouts for container operations
5. **Error Messages**: Provide clear failure messages with context

### CI/CD Integration

1. **Fast Feedback**: Keep basic integration tests in main workflow
2. **Resource Management**: Use cleanup strategies to prevent resource leaks
3. **Artifact Retention**: Balance storage costs with debugging needs
4. **Parallel Safety**: Ensure tests can run safely in parallel
5. **Environment Consistency**: Use consistent container versions

### Development Workflow

1. **Local First**: Test locally before pushing
2. **Incremental**: Run specific test suites during development
3. **Debug Ready**: Use no-cleanup mode for troubleshooting
4. **Resource Cleanup**: Always cleanup containers after development
5. **Documentation**: Update this README when changing test infrastructure

## Contributing

When adding new integration tests:

1. **Follow Naming Conventions**: Use `*IntegrationTests.cs` for test files
2. **Use Appropriate Fixtures**: Leverage existing fixtures or create new ones
3. **Add Categories**: Use `[Trait("Category", "...")]` for proper filtering
4. **Update CI/CD**: Modify workflows if new infrastructure is needed
5. **Document Changes**: Update this README and inline documentation
