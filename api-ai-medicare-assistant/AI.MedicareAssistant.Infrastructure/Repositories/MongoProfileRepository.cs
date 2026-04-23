using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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
    private readonly ILogger<MongoProfileRepository> _logger;

    public MongoProfileRepository(MongoDbContext context, ILogger<MongoProfileRepository> logger)
    {
        _collection = context.Users;
        _logger = logger;
    }

    public async Task<UserDocument?> GetByUserIdAsync(Guid userId) =>
        await _collection.Find(u => u.UserId == userId).FirstOrDefaultAsync();

    public async Task<UserDocument> CreateAsync(UserDocument entity)
    {
        // Profile creation is an update on the existing user document
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsProfileComplete = true;
        await EnsureIdAsync(entity);
        await _collection.ReplaceOneAsync(u => u.UserId == entity.UserId, entity);
        return entity;
    }

    public async Task UpdateAsync(UserDocument entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await EnsureIdAsync(entity);
        await _collection.ReplaceOneAsync(u => u.UserId == entity.UserId, entity);
    }

    private async Task EnsureIdAsync(UserDocument entity)
    {
        if (string.IsNullOrEmpty(entity.Id))
        {
            var existingId = await _collection
                .Find(u => u.UserId == entity.UserId)
                .Project(u => u.Id)
                .FirstOrDefaultAsync();
            entity.Id = existingId ?? ObjectId.GenerateNewId().ToString();
        }
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId) =>
        await _collection.Find(u => u.UserId == userId && u.IsProfileComplete).AnyAsync();
}
