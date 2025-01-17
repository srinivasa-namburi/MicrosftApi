using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Repositories;

public class DynamicDocumentProcessDefinitionRepository : GenericRepository<DynamicDocumentProcessDefinition>
{
    private const string CacheKeyAll = "DynamicDocumentProcessDefinition";
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

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

            var dynamicDefinitions = await GetAllAsync(useCache: false);
            await Cache.StringSetAsync(CacheKeyAll, JsonSerializer.Serialize(dynamicDefinitions), CacheDuration);
            return dynamicDefinitions;
        }
        else
        {
            return await AllRecords().Include(x=>x.DocumentOutline).ToListAsync();
        }
    }

    public new async Task<List<DynamicDocumentProcessDefinition>> GetAllAsync(bool useCache = false)
    {
        if (useCache)
        {
            var cachedData = await Cache.StringGetAsync(CacheKeyAll);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<List<DynamicDocumentProcessDefinition>>(cachedData);
            }

            var entities = await _dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .ToListAsync();
            await Cache.StringSetAsync(CacheKeyAll, JsonSerializer.Serialize(entities), CacheDuration);
            return entities;
        }
        else
        {
            return await _dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .ToListAsync();
        }
    }

    public async Task<DynamicDocumentProcessDefinition?> GetByShortNameAsync(string shortName, bool useCache = true)
    {
        if (useCache)
        {
            var cacheKey = $"DynamicDocumentProcessDefinition_ShortName_{shortName}";
            var cachedData = await Cache.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<DynamicDocumentProcessDefinition>(cachedData);
            }

            var dynamicDefinition = await AllRecords()
                .Where(x => x.ShortName == shortName)
                .Include(o=>o.DocumentOutline)
                .FirstOrDefaultAsync();

            if (dynamicDefinition != null)
            {
                await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(dynamicDefinition), CacheDuration);
                
                var cacheKeyId = $"{nameof(DynamicDocumentProcessDefinition)}_{dynamicDefinition.Id}";
                await Cache.StringSetAsync(cacheKeyId, JsonSerializer.Serialize(dynamicDefinition), CacheDuration);
                return dynamicDefinition;
            }

            return null;
        }
        else
        {
            return await AllRecords()
                .Where(x => x.ShortName == shortName)
                .Include(o => o.DocumentOutline)
                .FirstOrDefaultAsync();
        }
    }

    public new async Task AddAsync(DynamicDocumentProcessDefinition newDefinition, bool saveChanges = true)
    {
        await base.AddAsync(newDefinition, saveChanges);

        // Additionally create a cache item for the short name
        var cacheKey = $"DynamicDocumentProcessDefinition_ShortName_{newDefinition.ShortName}";
        await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(newDefinition), CacheDuration);

        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
    }

    public virtual new async Task UpdateAsync(
        DynamicDocumentProcessDefinition updatedDefinition, bool saveChanges = true)
    {
        await base.UpdateAsync(updatedDefinition, saveChanges);

        // Additionally update the cache item for the short name
        var cacheKey = $"DynamicDocumentProcessDefinition_ShortName_{updatedDefinition.ShortName}";
        await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(updatedDefinition), CacheDuration);

        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);

        // Invalidate the cache for the document outline as well
        if (updatedDefinition.DocumentOutline != null && updatedDefinition.DocumentOutline.Id != Guid.Empty)
        {
            var documentOutlineCacheKey = $"{nameof(DocumentOutline)}_{updatedDefinition.DocumentOutline.Id}";
            await Cache.KeyDeleteAsync(documentOutlineCacheKey);
        }
    }

    public new async Task DeleteAsync(DynamicDocumentProcessDefinition definition, bool saveChanges = true)
    {

        var fullDefinition = await _dbContext.DynamicDocumentProcessDefinitions
            .Include(p => p.Prompts)
            .Include(d=> d.DocumentOutline)
            .ThenInclude(documentOutline => documentOutline.OutlineItems)
                .ThenInclude(y => y.Children)
                    .ThenInclude(v=>v.Children)
                        .ThenInclude(w=>w.Children)
                            .ThenInclude(x=>x.Children)
            .FirstOrDefaultAsync(x => x.Id == definition.Id);

        // Recursively delete the document outline items, starting with leaf nodes (use a recursive method to delete the children first)
        if (fullDefinition.DocumentOutline != null)
        {
            var outlineItems = fullDefinition.DocumentOutline.OutlineItems;
            foreach (var item in outlineItems)
            {
                await DeleteDocumentOutlineItem(item);
            }
        }

        // This also deletes the cache key for the id, as well as the prompts and the document outline
        // It also executes the delete operation for the outline items which have been removed from the context
        await base.DeleteAsync(fullDefinition, saveChanges);
        
        // Additionally delete the cache item for the short name
        var cacheKey = $"DynamicDocumentProcessDefinition_ShortName_{definition.ShortName}";
        await Cache.KeyDeleteAsync(cacheKey);

        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
        
        // Invalidate the cache for the document outline as well
        if (definition.DocumentOutline != null && definition.DocumentOutline.Id != Guid.Empty)
        {
            var documentOutlineCacheKey = $"{nameof(DocumentOutline)}_{definition.DocumentOutline.Id}";
            await Cache.KeyDeleteAsync(documentOutlineCacheKey);


        }
    }

    private async Task DeleteDocumentOutlineItem(DocumentOutlineItem item)
    {
        if (item.Children != null)
        {
            foreach (var child in item.Children)
            {
                await DeleteDocumentOutlineItem(child);
            }
        }

        _dbContext.DocumentOutlineItems.Remove(item);
    }
}
