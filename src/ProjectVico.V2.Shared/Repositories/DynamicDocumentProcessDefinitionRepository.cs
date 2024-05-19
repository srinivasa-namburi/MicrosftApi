using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;
using StackExchange.Redis;

namespace ProjectVico.V2.Shared.Repositories;

public class DynamicDocumentProcessDefinitionRepository : GenericRepository<DynamicDocumentProcessDefinition>
{
    private const string CacheKeyAll = "DynamicDocumentProcessDefinitions";
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(15);

    public DynamicDocumentProcessDefinitionRepository(
        DocGenerationDbContext dbContext,
        IConnectionMultiplexer redisConnection
        )
        : base(dbContext, redisConnection)
    {
        SetCacheDuration(DefaultCacheDuration);
    }

    public async Task<List<DynamicDocumentProcessDefinition>> GetAllDynamicDocumentProcessDefinitionsAsync(bool useCache = true)
    {
        if (useCache)
        {
            var cachedData = await Cache.StringGetAsync(CacheKeyAll);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<List<DynamicDocumentProcessDefinition>>(cachedData);
            }

            var dynamicDefinitions = await AllRecords().ToListAsync();
            await Cache.StringSetAsync(CacheKeyAll, JsonSerializer.Serialize(dynamicDefinitions), CacheDuration);
            return dynamicDefinitions;
        }
        else
        {
            return await AllRecords().ToListAsync();
        }
    }

    public async Task<DynamicDocumentProcessDefinition?> GetByShortNameAsync(string shortName, bool useCache = true)
    {
        if (useCache)
        {
            var cacheKey = $"DynamicDocumentProcessDefinitions:{shortName}";
            var cachedData = await Cache.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<DynamicDocumentProcessDefinition>(cachedData);
            }

            var dynamicDefinition = await AllRecords()
                .Where(x => x.ShortName == shortName)
                .FirstOrDefaultAsync();

            if (dynamicDefinition != null)
            {
                await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(dynamicDefinition), CacheDuration);
                return dynamicDefinition;
            }

            return null;
        }
        else
        {
            return await AllRecords()
                .Where(x => x.ShortName == shortName)
                .FirstOrDefaultAsync();
        }
    }

    public new async Task AddAsync(DynamicDocumentProcessDefinition newDefinition)
    {
        await base.AddAsync(newDefinition);
        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
    }

    public new async Task UpdateAsync(DynamicDocumentProcessDefinition updatedDefinition)
    {
        await base.UpdateAsync(updatedDefinition);
        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
    }
}