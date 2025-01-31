using System.Text.Json;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Repositories;

/// <summary>
/// A generic repository for managing entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the entity.</typeparam>
public class GenericRepository<T>: IGenericRepository<T> where T : EntityBase
{
    /// <summary>
    /// The database context.
    /// </summary>
    protected readonly DocGenerationDbContext _dbContext;

    /// <summary>
    /// The Redis cache database.
    /// </summary>
    protected readonly IDatabase Cache;

    /// <summary>
    /// The default cache duration.
    /// </summary>
    protected TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericRepository{T}"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="redisConnection">The Redis connection multiplexer.</param>
    public GenericRepository(
        DocGenerationDbContext dbContext,
        IConnectionMultiplexer redisConnection)
    {
        _dbContext = dbContext;
        Cache = redisConnection.GetDatabase();
    }

    /// <summary>
    /// Sets the cache duration for the repository.
    /// </summary>
    /// <param name="cacheDuration">The cache duration.</param>
    public void SetCacheDuration(TimeSpan cacheDuration)
    {
        CacheDuration = cacheDuration;
    }

    /// <summary>
    /// Gets all records of type <typeparamref name="T"/> as an <see cref="IQueryable{T}"/>.
    /// </summary>
    /// <returns>An <see cref="IQueryable{T}"/> of all records.</returns>
    public virtual IQueryable<T> AllRecords()
    {
        return _dbContext.Set<T>().AsNoTracking().AsQueryable();
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllAsync(bool useCache = false)
    {
        if (useCache)
        {
            var cachedData = await Cache.StringGetAsync(typeof(T).Name);
            if (cachedData.HasValue)
            {
                var result = JsonSerializer.Deserialize<List<T>>(cachedData!);
                if (result != null)
                {
                    return result;
                }
            }

            var entities = await _dbContext.Set<T>().AsNoTracking().ToListAsync();
            await Cache.StringSetAsync(typeof(T).Name, JsonSerializer.Serialize(entities), CacheDuration);
            return entities;
        }
        else
        {
            return await _dbContext.Set<T>().ToListAsync();
        }
    }

    /// <inheritdoc/>
    public virtual async Task<T?> GetByIdAsync(Guid id, bool useCache = true)
    {
        if (useCache)
        {
            var cacheKey = $"{typeof(T).Name}_{id}";
            var cachedData = await Cache.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                var result = JsonSerializer.Deserialize<T>(cachedData!);

                if (result != null)
                {
                    return result;
                }
            }

            var entity = await _dbContext.Set<T>().FindAsync(id);
            if (entity != null)
            {
                await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(entity), CacheDuration);
            }
            return entity;
        }
        else
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }
    }

    /// <inheritdoc/>
    public virtual async Task AddAsync(T entity, bool saveChanges = true)
    {
        await _dbContext.Set<T>().AddAsync(entity);

        if (saveChanges)
        {
            await SaveChangesAsync();
        }

        if (CacheDuration > 0.Seconds())
        {
            // Cache the newly added entity
            var cacheKey = $"{typeof(T).Name}_{entity.Id}";
            await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(entity), CacheDuration);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(T entity, bool saveChanges = true)
    {
        _dbContext.Set<T>().Update(entity);
        if (saveChanges)
        {
            await SaveChangesAsync();
        }

        if (CacheDuration > 0.Seconds())
        {
            // Update the cache with the updated entity
            var cacheKey = $"{typeof(T).Name}_{entity.Id}";
            await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(entity), CacheDuration);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(T entity, bool saveChanges = true)
    {
        var cacheKey = $"{typeof(T).Name}_{entity.Id}";

        _dbContext.Set<T>().Remove(entity);

        if (saveChanges)
        {
            await SaveChangesAsync();
        }

        // Remove the entity from the cache if it exists
        await Cache.KeyDeleteAsync(cacheKey);
    }

    /// <inheritdoc/>
    public virtual async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}

