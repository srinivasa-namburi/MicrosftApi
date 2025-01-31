using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Repositories;

/// <summary>
/// An interface for a generic repository for managing entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the entity</typeparam>
public interface IGenericRepository<T> where T : EntityBase
{
    /// <summary>
    /// Gets an entity of type <typeparamref name="T"/> by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="useCache">Whether to use the cache.</param>
    /// <returns>
    /// An entity of type <typeparamref name="T"/> with the specified identifier, or null if not found.
    /// </returns>
    Task<T?> GetByIdAsync(Guid id, bool useCache = true);

    /// <summary>
    /// Gets all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="useCache">Whether to use the cache.</param>
    /// <returns>
    /// A list of entities of type <typeparamref name="T"/>.
    /// </returns>
    Task<List<T>> GetAllAsync(bool useCache = false);

    /// <summary>
    /// Adds a new entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="saveChanges">Whether to save changes to the database.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous add operation.
    /// </returns>
    Task AddAsync(T entity, bool saveChanges = true);

    /// <summary>
    /// Updates an existing entity of type <typeparamref name="T"/> in the repository.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="saveChanges">Whether to save changes to the database.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous update operation.</returns>
    Task UpdateAsync(T entity, bool saveChanges = true);

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> from the repository.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="saveChanges">Whether to save changes to the database.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous delete operation.</returns>
    Task DeleteAsync(T entity, bool saveChanges = true);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous save operation.</returns>
    Task SaveChangesAsync();
}
