namespace Shared.TestInfrastructure.Builders;

/// <summary>
/// Base class for test data builders using the builder pattern
/// </summary>
/// <typeparam name="T">The type being built</typeparam>
/// <typeparam name="TBuilder">The builder type (for fluent chaining)</typeparam>
public abstract class TestDataBuilder<T, TBuilder>
    where TBuilder : TestDataBuilder<T, TBuilder>
    where T : class
{
    /// <summary>
    /// Build the object with the current configuration
    /// </summary>
    public abstract T Build();

    /// <summary>
    /// Return this builder cast to the derived type for fluent chaining
    /// </summary>
    protected TBuilder This => (TBuilder)this;

    /// <summary>
    /// Create multiple instances of the object
    /// </summary>
    public List<T> BuildMany(int count)
    {
        var items = new List<T>();
        for (int i = 0; i < count; i++)
        {
            items.Add(Build());
        }
        return items;
    }

    /// <summary>
    /// Create multiple instances with different configurations
    /// </summary>
    public List<T> BuildMany(int count, Action<TBuilder, int> configurator)
    {
        var items = new List<T>();
        for (int i = 0; i < count; i++)
        {
            var builder = CreateNew();
            configurator(builder, i);
            items.Add(builder.Build());
        }
        return items;
    }

    /// <summary>
    /// Create a new instance of the builder (must be implemented by derived classes)
    /// </summary>
    protected abstract TBuilder CreateNew();
}

/// <summary>
/// Interface for builders that can generate test data with realistic values
/// </summary>
public interface IRealisticTestDataBuilder
{
    /// <summary>
    /// Configure the builder with realistic test data
    /// </summary>
    void WithRealisticData();
}
