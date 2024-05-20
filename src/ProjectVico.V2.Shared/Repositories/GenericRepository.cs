using System.Text.Json;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;
using StackExchange.Redis;

namespace ProjectVico.V2.Shared.Repositories;

public class GenericRepository<T> where T : EntityBase
{
    private readonly DocGenerationDbContext _dbContext;
    protected readonly IDatabase Cache;
    protected TimeSpan CacheDuration = TimeSpan.FromMinutes(0);

    public GenericRepository(
        DocGenerationDbContext dbContext,
        IConnectionMultiplexer redisConnection)
    {
        _dbContext = dbContext;
        Cache = redisConnection.GetDatabase();
    }

    protected void SetCacheDuration(TimeSpan cacheDuration)
    {
        CacheDuration = cacheDuration;
    }

    public virtual IQueryable<T> AllRecords()
    {
        return _dbContext.Set<T>().AsQueryable();
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

    public async Task AddAsync(T entity)
    {
        await _dbContext.Set<T>().AddAsync(entity);
        await _dbContext.SaveChangesAsync();
        
        // Cache the newly added entity
        var cacheKey = $"{typeof(T).Name}_{entity.Id}";
        await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(entity), CacheDuration);
    }

    public async Task UpdateAsync(T entity)
    {
        _dbContext.Set<T>().Update(entity);
        await _dbContext.SaveChangesAsync();
        
        // Update the cache with the updated entity
        var cacheKey = $"{typeof(T).Name}_{entity.Id}";
        await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(entity), CacheDuration);
    }

    public async Task DeleteAsync(T entity)
    {
        var cacheKey = $"{typeof(T).Name}_{entity.Id}";

        _dbContext.Set<T>().Remove(entity);
        await _dbContext.SaveChangesAsync();

        // Remove the entity from the cache if it exists
        await Cache.KeyDeleteAsync(cacheKey);
    }
}