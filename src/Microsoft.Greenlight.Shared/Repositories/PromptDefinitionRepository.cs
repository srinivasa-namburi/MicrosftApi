using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Services.Caching;

namespace Microsoft.Greenlight.Shared.Repositories;

/// <summary>
/// Repository for managing <see cref="PromptDefinition"/> entities.
/// </summary>
public class PromptDefinitionRepository : GenericRepository<PromptDefinition>
{
    private const string CacheKeyAll = "Repo:PromptDefinition:All";
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptDefinitionRepository"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="appCache">The centralized cache.</param>
    public PromptDefinitionRepository(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IAppCache appCache
    ) : base(dbContextFactory, appCache)
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
            return await _appCache.GetOrCreateAsync(
                CacheKeyAll,
                async ct => await GetAllAsync(useCache: false),
                CacheDuration,
                allowDistributed: false);
        }
        else
        {
            return await AllRecords().Include(x => x.Variables).ToListAsync();
        }
    }

    /// <inheritdoc/>
    public virtual new async Task<List<PromptDefinition>> GetAllAsync(bool useCache = false)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        if (useCache)
        {
            return await _appCache.GetOrCreateAsync(
                CacheKeyAll,
                async ct => await dbContext.PromptDefinitions
                    .Include(x => x.Variables)
                    .ToListAsync(ct),
                CacheDuration,
                allowDistributed: false);
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
            var cacheKey = $"Repo:PromptDefinition:ShortCode:{shortCode}";
            return await _appCache.GetOrCreateAsync(
                cacheKey,
                async ct => await AllRecords()
                    .Where(x => x.ShortCode == shortCode)
                    .FirstOrDefaultAsync(ct),
                CacheDuration,
                allowDistributed: true);
        }
        else
        {
            return await AllRecords()
                .Where(x => x.ShortCode == shortCode)
                .FirstOrDefaultAsync();
        }
    }

    /// <inheritdoc/>
    public new virtual async Task AddAsync(PromptDefinition newDefinition, bool saveChanges = true)
    {
        await base.AddAsync(newDefinition, saveChanges);

        // Additionally update the short code cache
        var cacheKey = $"Repo:PromptDefinition:ShortCode:{newDefinition.ShortCode}";
        await _appCache.SetAsync(cacheKey, newDefinition, CacheDuration, allowDistributed: true);

        // Invalidate the full cache by removing it
        await _appCache.RemoveAsync(CacheKeyAll);
    }

    /// <inheritdoc/>
    public new async Task UpdateAsync(PromptDefinition updatedDefinition, bool saveChanges = true)
    {
        await base.UpdateAsync(updatedDefinition, saveChanges);
        // Invalidate the full cache
        await _appCache.RemoveAsync(CacheKeyAll);
    }
}
