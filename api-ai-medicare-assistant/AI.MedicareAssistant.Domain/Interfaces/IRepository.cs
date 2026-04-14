using Domain.Entities;

namespace Domain.Interfaces;

/// <summary>
/// Generic repository interface for basic CRUD operations.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<T?> GetByUserIdAsync(Guid userId);
    Task<T> CreateAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<bool> ExistsByUserIdAsync(Guid userId);
}
