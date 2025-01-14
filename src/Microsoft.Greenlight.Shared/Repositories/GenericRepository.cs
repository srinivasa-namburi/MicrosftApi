using System.Text.Json;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Repositories;

public class GenericRepository<T> where T : EntityBase
{
    protected readonly DocGenerationDbContext _dbContext;
    protected readonly IDatabase Cache;
    protected TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public GenericRepository(
        DocGenerationDbContext dbContext,
        IConnectionMultiplexer redisConnection)
    {
        _dbContext = dbContext;
        Cache = redisConnection.GetDatabase();
    }

    public void SetCacheDuration(TimeSpan cacheDuration)
    {
        CacheDuration = cacheDuration;
    }

    public virtual IQueryable<T> AllRecords()
    {
        return _dbContext.Set<T>().AsNoTracking().AsQueryable();
    }

    public async Task<List<T>> GetAllAsync(bool useCache = false)
    {
        if (useCache)
        {
            var cachedData = await Cache.StringGetAsync(typeof(T).Name);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<List<T>>(cachedData);
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

    public async Task<T?> GetByIdAsync(Guid id, bool useCache = true)
    {
        if (useCache)
        {
            var cacheKey = $"{typeof(T).Name}_{id}";
            var cachedData = await Cache.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<T>(cachedData);
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

    public async Task AddAsync(T entity, bool saveChanges=true)
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

    public async Task UpdateAsync(T entity, bool saveChanges=true)
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

    public virtual async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }


}
