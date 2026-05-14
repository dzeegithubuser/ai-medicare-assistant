using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

/// <summary>
/// MongoDB implementation of <see cref="IProfileRepository"/> against the <c>userProfiles</c> collection.
/// </summary>
public class MongoProfileRepository : IProfileRepository
{
    private readonly IMongoCollection<ProfileDocument> _collection;
    private readonly ILogger<MongoProfileRepository> _logger;

    public MongoProfileRepository(MongoDbContext context, ILogger<MongoProfileRepository> logger)
    {
        _collection = context.UserProfiles;
        _logger = logger;
    }

    public async Task<ProfileDocument?> GetByUserIdAsync(Guid userId) =>
        await _collection.Find(p => p.UserId == userId).FirstOrDefaultAsync();

    public async Task<ProfileDocument> CreateAsync(ProfileDocument entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(entity.Id))
            entity.Id = ObjectId.GenerateNewId().ToString();
        await _collection.InsertOneAsync(entity);
        return entity;
    }

    public async Task UpdateAsync(ProfileDocument entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(entity.Id))
        {
            var existingId = await _collection
                .Find(p => p.UserId == entity.UserId)
                .Project(p => p.Id)
                .FirstOrDefaultAsync();
            entity.Id = existingId ?? ObjectId.GenerateNewId().ToString();
        }
        await _collection.ReplaceOneAsync(
            p => p.UserId == entity.UserId,
            entity,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId) =>
        await _collection.Find(p => p.UserId == userId && p.IsProfileComplete).AnyAsync();

    public async Task DeleteByUserIdAsync(Guid userId) =>
        await _collection.DeleteManyAsync(p => p.UserId == userId);
}
