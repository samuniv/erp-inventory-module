using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Shared.TestInfrastructure.Utilities;

/// <summary>
/// Utilities for database testing scenarios
/// </summary>
public static class DatabaseTestUtilities
{
    /// <summary>
    /// Wait for an entity to appear in the database
    /// </summary>
    public static async Task<T?> WaitForEntityAsync<T>(
        DbContext context,
        Expression<Func<T, bool>> predicate,
        TimeSpan timeout,
        ILogger? logger = null) where T : class
    {
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var entity = await context.Set<T>().FirstOrDefaultAsync(predicate);
                if (entity != null)
                {
                    logger?.LogInformation("Found entity of type {EntityType} matching predicate", typeof(T).Name);
                    return entity;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error querying for entity of type {EntityType}", typeof(T).Name);
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        logger?.LogWarning("Entity of type {EntityType} not found within timeout", typeof(T).Name);
        return null;
    }

    /// <summary>
    /// Wait for a specific count of entities in the database
    /// </summary>
    public static async Task<List<T>> WaitForEntitiesAsync<T>(
        DbContext context,
        Expression<Func<T, bool>>? predicate,
        int expectedCount,
        TimeSpan timeout,
        ILogger? logger = null) where T : class
    {
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var query = context.Set<T>().AsQueryable();
                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                var entities = await query.ToListAsync();
                if (entities.Count >= expectedCount)
                {
                    logger?.LogInformation("Found {Count} entities of type {EntityType}", entities.Count, typeof(T).Name);
                    return entities;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error querying for entities of type {EntityType}", typeof(T).Name);
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        logger?.LogWarning("Expected {ExpectedCount} entities of type {EntityType} not found within timeout",
            expectedCount, typeof(T).Name);
        return new List<T>();
    }

    /// <summary>
    /// Clean all data from specified entity types
    /// </summary>
    public static async Task CleanDatabaseAsync<T>(DbContext context, ILogger? logger = null) where T : class
    {
        try
        {
            var entities = await context.Set<T>().ToListAsync();
            if (entities.Any())
            {
                context.Set<T>().RemoveRange(entities);
                await context.SaveChangesAsync();
                logger?.LogInformation("Cleaned {Count} entities of type {EntityType}", entities.Count, typeof(T).Name);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error cleaning entities of type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Clean all data from multiple entity types
    /// </summary>
    public static async Task CleanDatabaseAsync(DbContext context, ILogger? logger = null, params Type[] entityTypes)
    {
        foreach (var entityType in entityTypes)
        {
            try
            {
                var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes);
                var genericSetMethod = setMethod!.MakeGenericMethod(entityType);
                var dbSet = genericSetMethod.Invoke(context, null);

                var toListMethod = typeof(EntityFrameworkQueryableExtensions)
                    .GetMethods()
                    .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 1)
                    .MakeGenericMethod(entityType);

                var entitiesTask = (Task)toListMethod.Invoke(null, new[] { dbSet })!;
                await entitiesTask;

                var entitiesProperty = entitiesTask.GetType().GetProperty("Result");
                var entities = (System.Collections.IEnumerable)entitiesProperty!.GetValue(entitiesTask)!;

                var entitiesList = entities.Cast<object>().ToList();
                if (entitiesList.Any())
                {
                    var removeRangeMethod = typeof(DbSet<>).MakeGenericType(entityType)
                        .GetMethod(nameof(DbSet<object>.RemoveRange), new[] { typeof(IEnumerable<>).MakeGenericType(entityType) });

                    removeRangeMethod!.Invoke(dbSet, new[] { entities });

                    logger?.LogInformation("Cleaned {Count} entities of type {EntityType}", entitiesList.Count, entityType.Name);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error cleaning entities of type {EntityType}", entityType.Name);
                throw;
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Execute a database operation with retry logic
    /// </summary>
    public static async Task<T> WithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan delay = default,
        ILogger? logger = null)
    {
        if (delay == default)
            delay = TimeSpan.FromMilliseconds(500);

        Exception lastException = null!;

        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (i == maxRetries)
                {
                    logger?.LogError(ex, "Database operation failed after {MaxRetries} retries", maxRetries);
                    throw;
                }

                logger?.LogWarning(ex, "Database operation failed, retrying ({Attempt}/{MaxRetries})", i + 1, maxRetries);
                await Task.Delay(delay);
            }
        }

        throw lastException;
    }
}
