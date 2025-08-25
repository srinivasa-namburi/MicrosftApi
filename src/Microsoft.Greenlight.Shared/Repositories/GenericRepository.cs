using System.Text.Json;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.Caching;

namespace Microsoft.Greenlight.Shared.Repositories;

/// <summary>
/// A generic repository for managing entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the entity.</typeparam>
public class GenericRepository<T>: IGenericRepository<T> where T : EntityBase
{
    /// <summary>
    /// Shared database context factory for the repository
    /// </summary>
    protected readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

    /// <summary>
    /// Centralized cache.
    /// </summary>
    protected readonly IAppCache _appCache;

    /// <summary>
    /// The default cache duration.
    /// </summary>
    protected TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static string CacheKeyAll => $"Repo:{typeof(T).Name}:All";
    private static string CacheKeyForId(Guid id) => $"Repo:{typeof(T).Name}:{id}";

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericRepository{T}"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="appCache">The centralized cache.</param>
    public GenericRepository(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IAppCache appCache)
    {
        _dbContextFactory = dbContextFactory;
        _appCache = appCache;
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
        var dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.Set<T>().AsNoTracking().AsQueryable();
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllAsync(bool useCache = false)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        if (useCache)
        {
            // Avoid pushing large lists to Redis: keep LocalOnly
            return await _appCache.GetOrCreateAsync(
                CacheKeyAll,
                async ct => await dbContext.Set<T>().AsNoTracking().ToListAsync(ct),
                CacheDuration,
                allowDistributed: false);
        }
        else
        {
            return await dbContext.Set<T>().ToListAsync();
        }
    }

    /// <inheritdoc/>
    public virtual async Task<T?> GetByIdAsync(Guid id, bool useCache = true)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        if (useCache)
        {
            var key = CacheKeyForId(id);
            return await _appCache.GetOrCreateAsync(
                key,
                async ct => await dbContext.Set<T>().FindAsync([id], ct).AsTask(),
                CacheDuration,
                allowDistributed: true);
        }
        else
        {
            return await dbContext.Set<T>().FindAsync(id);
        }
    }

    /// <inheritdoc/>
    public virtual async Task AddAsync(T entity, bool saveChanges = true)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        await dbContext.Set<T>().AddAsync(entity);

        if (saveChanges)
        {
            await SaveChangesAsync();
        }

        if (CacheDuration > 0.Seconds())
        {
            // Cache the newly added entity (size-checked by IAppCache)
            var cacheKey = CacheKeyForId(entity.Id);
            await _appCache.SetAsync(cacheKey, entity, CacheDuration, allowDistributed: true);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(T entity, bool saveChanges = true)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        dbContext.Set<T>().Update(entity);
        if (saveChanges)
        {
            await SaveChangesAsync();
        }

        if (CacheDuration > 0.Seconds())
        {
            // Update the cache with the updated entity
            var cacheKey = CacheKeyForId(entity.Id);
            await _appCache.SetAsync(cacheKey, entity, CacheDuration, allowDistributed: true);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(T entity, bool saveChanges = true)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var cacheKey = CacheKeyForId(entity.Id);

        dbContext.Set<T>().Remove(entity);

        if (saveChanges)
        {
            await SaveChangesAsync();
        }

        // Remove the entity from the cache if it exists
        await _appCache.RemoveAsync(cacheKey);
    }

    /// <inheritdoc/>
    public virtual async Task SaveChangesAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.SaveChangesAsync();
    }
}

