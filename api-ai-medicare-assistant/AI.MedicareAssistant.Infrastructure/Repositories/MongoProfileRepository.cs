using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

/// <summary>
/// MongoDB implementation of <see cref="IProfileRepository"/>.
/// Profile fields are embedded directly in <see cref="UserDocument"/>; this repository
/// delegates to the <c>users</c> collection.
/// </summary>
public class MongoProfileRepository : IProfileRepository
{
    private readonly IMongoCollection<UserDocument> _collection;

    public MongoProfileRepository(MongoDbContext context) =>
        _collection = context.Users;

    public async Task<UserDocument?> GetByUserIdAsync(Guid userId) =>
        await _collection.Find(u => u.UserId == userId).FirstOrDefaultAsync();

    public async Task<UserDocument> CreateAsync(UserDocument entity)
    {
        // Profile creation is an update on the existing user document
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsProfileComplete = true;
        await _collection.ReplaceOneAsync(u => u.UserId == entity.UserId, entity);
        return entity;
    }

    public async Task UpdateAsync(UserDocument entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(u => u.UserId == entity.UserId, entity);
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId) =>
        await _collection.Find(u => u.UserId == userId && u.IsProfileComplete).AnyAsync();
}
