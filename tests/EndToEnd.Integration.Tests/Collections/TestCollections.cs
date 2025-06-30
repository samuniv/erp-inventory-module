using Xunit;

namespace EndToEnd.Integration.Tests.Collections;

/// <summary>
/// xUnit collection definition for end-to-end integration tests
/// </summary>
[CollectionDefinition("End-to-End Integration Tests")]
public class EndToEndIntegrationTestCollection : ICollectionFixture<Fixtures.EndToEndTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
