using System.Linq.Expressions;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Repositories;

public interface IGenericRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(Guid id, bool includeDependents = false);
    Task<IQueryable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<T> DeleteAsync(Guid id);
    Task<T> DeleteAsync(T entity);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<bool> AnyAsync();
}
