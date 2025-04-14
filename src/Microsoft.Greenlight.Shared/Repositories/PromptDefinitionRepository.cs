using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Repositories;

/// <summary>
/// Repository for managing <see cref="PromptDefinition"/> entities.
/// </summary>
public class PromptDefinitionRepository : GenericRepository<PromptDefinition>
{
    private const string CacheKeyAll = "PromptDefinition";
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptDefinitionRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="redisConnection">The Redis connection.</param>
    public PromptDefinitionRepository(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IConnectionMultiplexer redisConnection
    ) : base(dbContextFactory, redisConnection)
    {
        SetCacheDuration(DefaultCacheDuration);
    }

    /// <summary>
    /// Gets all prompt definitions.
    /// </summary>
    /// <param name="useCache">Whether to use cache.</param>
    /// <returns>
    /// A list of prompt definitions.
    /// </returns>
    public virtual async Task<List<PromptDefinition>> GetAllPromptDefinitionsAsync(bool useCache = true)
    {
        if (useCache)
        {
            var cachedData = await Cache.StringGetAsync(CacheKeyAll);
            if (cachedData.HasValue)
            {
                var result = JsonSerializer.Deserialize<List<PromptDefinition>>(cachedData!);
                if (result != null)
                {
                    return result;
                }
            }

            var promptDefinitions = await GetAllAsync(useCache: false);
            await Cache.StringSetAsync(CacheKeyAll, JsonSerializer.Serialize(promptDefinitions), CacheDuration);
            return promptDefinitions;
        }
        else
        {
            return await AllRecords().Include(x => x.Variables).ToListAsync();
        }
    }

    /// <inheritdoc/>
    public virtual new async Task<List<PromptDefinition>> GetAllAsync(bool useCache = false)
    {
        var dbContext = await _dbContextFactory.CreateDbContextAsync();

        if (useCache)
        {
            var cachedData = await Cache.StringGetAsync(CacheKeyAll);
            if (cachedData.HasValue)
            {
                var result = JsonSerializer.Deserialize<List<PromptDefinition>>(cachedData!);
                if (result != null)
                {
                    return result;
                }
            }

            var entities = await dbContext.PromptDefinitions
                .Include(x => x.Variables)
                .ToListAsync();
            await Cache.StringSetAsync(CacheKeyAll, JsonSerializer.Serialize(entities), CacheDuration);
            return entities;
        }
        else
        {
            return await dbContext.PromptDefinitions
                .Include(x => x.Variables)
                .ToListAsync();
        }
    }

    /// <summary>
    /// Gets a prompt definition by its short code.
    /// </summary>
    /// <param name="shortCode">The short code of the prompt definition.</param>
    /// <param name="useCache">Whether to use cache.</param>
    /// <returns>
    /// The prompt definition if found; otherwise, null.
    /// </returns>
    public async Task<PromptDefinition?> GetByShortCodeAsync(string shortCode, bool useCache = true)
    {
        if (useCache)
        {
            var cacheKey = $"{nameof(PromptDefinition)}_{shortCode}";
            var cachedData = await Cache.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<PromptDefinition>(cachedData!);
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

    /// <inheritdoc/>
    public virtual new async Task AddAsync(PromptDefinition newDefinition, bool saveChanges = true)
    {
        await base.AddAsync(newDefinition, saveChanges);

        // Additionally create a cache item for the short code
        var cacheKey = $"{nameof(PromptDefinition)}_ShortCode_{newDefinition.ShortCode}";
        await Cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(newDefinition), CacheDuration);

        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
    }

    /// <inheritdoc/>
    public new async Task UpdateAsync(PromptDefinition updatedDefinition, bool saveChanges = true)
    {
        await base.UpdateAsync(updatedDefinition, saveChanges);
        // Invalidate the full cache
        await Cache.KeyDeleteAsync(CacheKeyAll);
    }
}
