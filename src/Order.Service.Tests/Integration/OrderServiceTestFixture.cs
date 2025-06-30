using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Order.Service.Data;
using Order.Service.Services;
using Moq;
using Shared.TestInfrastructure.Base;
using Xunit;

namespace Order.Service.Tests.Integration;

/// <summary>
/// Test fixture for Order Service integration tests using shared test infrastructure
/// </summary>
public class OrderServiceTestFixture : ServiceIntegrationTestBase<Program, OrderDbContext>
{
    public OrderServiceTestFixture() : base(usePostgreSql: true, useOracle: false, useKafka: true)
    {
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Mock the event publisher for testing
        var mockEventPublisher = new Mock<IEventPublisher>();
        mockEventPublisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Events.EventPublishResult.Success("test-topic", 0, 0));

        services.AddScoped(_ => mockEventPublisher.Object);

        // Override Kafka configuration for testing
        services.Configure<Events.KafkaOptions>(options =>
        {
            options.BootstrapServers = Infrastructure.GetKafkaBootstrapServers();
            options.GroupId = "test-order-service";
        });
    }

    protected override async Task SeedTestDataAsync(OrderDbContext context)
    {
        // Add any seed data needed for Order Service tests
        await Task.CompletedTask;
    }
}
