using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Repositories;

public class PromptDefinitionRepository : GenericRepository<PromptDefinition>
{
    private const string CacheKeyAll = "PromptDefinition";
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    public PromptDefinitionRepository(
        DocGenerationDbContext dbContext,
        IConnectionMultiplexer redisConnection
    )
        : base(dbContext, redisConnection)
    {
        SetCacheDuration(DefaultCacheDuration);
    }

    public async Task<List<PromptDefinition>> GetAllPromptDefinitionsAsync(bool useCache = true)
    {
        if (useCache)
        {
            var cachedData = await Cache.StringGetAsync(CacheKeyAll);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<List<PromptDefinition>>(cachedData);
            }

            var promptDefinitions = await GetAllAsync(useCache: false);
            await Cache.StringSetAsync(CacheKeyAll, JsonSerializer.Serialize(promptDefinitions), CacheDuration);
            return promptDefinitions;
        }
        else
        {
            return await AllRecords().Include(x=>x.Variables).ToListAsync();
        }
    }

    public async Task<PromptDefinition?> GetByShortCodeAsync(string shortCode, bool useCache = true)
    {
        if (useCache)
        {
            var cacheKey = $"{nameof(PromptDefinition)}_{shortCode}";
            var cachedData = await Cache.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<PromptDefinition>(cachedData);
            }

            var promptDefinition = await AllRecords()
                .Where(x => x.ShortCode == shortCode)
                .FirstOrDefaultAsync();

            if (promptDefinition != null)
            {
                await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(promptDefinition), CacheDuration);
                return promptDefinition;
            }

            return null;
        }
        else
        {
            return await AllRecords()
                .Where(x => x.ShortCode == shortCode)
                .FirstOrDefaultAsync();
        }
    }

    public new async Task AddAsync(PromptDefinition newDefinition, bool saveChanges = true)
    {
        await base.AddAsync(newDefinition, saveChanges);

        // Additionally create a cache item for the short code
        var cacheKey = $"{nameof(PromptDefinition)}_ShortCode_{newDefinition.ShortCode}";
        await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(newDefinition), CacheDuration);

        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
    }

    public new async Task UpdateAsync(PromptDefinition updatedDefinition, bool saveChanges = true)
    {
        await base.UpdateAsync(updatedDefinition, saveChanges);
        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
    }
}
