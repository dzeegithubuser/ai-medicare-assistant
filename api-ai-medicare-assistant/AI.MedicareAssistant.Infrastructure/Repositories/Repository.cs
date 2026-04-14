using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// Generic repository providing standard CRUD for any entity with a UserId FK.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Db;
    protected readonly DbSet<T> DbSet;

    public Repository(AppDbContext db)
    {
        Db = db;
        DbSet = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await DbSet.FindAsync(id);
    }

    public async Task<T?> GetByUserIdAsync(Guid userId)
    {
        // Convention: all profile entities have a "UserId" property
        return await DbSet.FirstOrDefaultAsync(
            e => EF.Property<Guid>(e, "UserId") == userId);
    }

    public async Task<T> CreateAsync(T entity)
    {
        DbSet.Add(entity);
        await Db.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        await Db.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        DbSet.Remove(entity);
        await Db.SaveChangesAsync();
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId)
    {
        return await DbSet.AnyAsync(
            e => EF.Property<Guid>(e, "UserId") == userId);
    }
}
