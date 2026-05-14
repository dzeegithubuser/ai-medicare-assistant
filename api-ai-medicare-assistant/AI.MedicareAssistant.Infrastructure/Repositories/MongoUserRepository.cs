using Domain.Constants;
using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

/// <summary>
/// MongoDB implementation of <see cref="IUserRepository"/> against the <c>users</c> collection.
/// </summary>
public class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _collection;
    private readonly ILogger<MongoUserRepository> _logger;

    public MongoUserRepository(MongoDbContext context, ILogger<MongoUserRepository> logger)
    {
        _collection = context.Users;
        _logger = logger;
    }

    public async Task<UserDocument?> GetByEmailAsync(string email) =>
        await _collection.Find(u => u.Email == email).FirstOrDefaultAsync();

    public async Task<UserDocument?> GetByPhoneAsync(string phone) =>
        await _collection.Find(u => u.Phone == phone).FirstOrDefaultAsync();

    public async Task<UserDocument?> GetByIdAsync(Guid id) =>
        await _collection.Find(u => u.UserId == id).FirstOrDefaultAsync();

    public async Task<UserDocument> CreateAsync(UserDocument user)
    {
        await _collection.InsertOneAsync(user);
        return user;
    }

    public async Task UpdateAsync(UserDocument user)
    {
        user.UpdatedAt = DateTime.UtcNow;

        if (string.IsNullOrEmpty(user.Id))
        {
            var existingId = await _collection
                .Find(u => u.UserId == user.UserId)
                .Project(u => u.Id)
                .FirstOrDefaultAsync();
            user.Id = existingId ?? ObjectId.GenerateNewId().ToString();
        }

        await _collection.ReplaceOneAsync(u => u.UserId == user.UserId, user);
    }

    public async Task DeleteAsync(Guid userId) =>
        await _collection.DeleteOneAsync(u => u.UserId == userId);

    public async Task<bool> EmailExistsAsync(string email) =>
        await _collection.Find(u => u.Email == email).AnyAsync();

    public async Task<bool> PhoneExistsAsync(string phone) =>
        await _collection.Find(u => u.Phone == phone).AnyAsync();

    public async Task<List<UserDocument>> GetByFpIdAsync(Guid fpUserId) =>
        await _collection.Find(u => u.FpId == fpUserId).ToListAsync();

    public async Task<List<UserDocument>> GetByFpgIdAndRoleAsync(Guid fpgId, string role) =>
        await _collection.Find(u => u.FpgId == fpgId && u.Role == role).ToListAsync();

    public async Task<List<UserDocument>> GetAllByRoleAsync(string role) =>
        await _collection.Find(u => u.Role == role).ToListAsync();

    public async Task<List<UserDocument>> GetEndUsersByFpgAsync(Guid fpgId)
    {
        var fpUserIds = await _collection
            .Find(u => u.FpgId == fpgId && u.Role == UserRoles.FinancialPlanner)
            .Project(u => u.UserId)
            .ToListAsync();

        if (fpUserIds.Count == 0)
            return new List<UserDocument>();

        var nullableFpIds = fpUserIds.Select(g => (Guid?)g).ToList();
        var filter = Builders<UserDocument>.Filter.Eq(u => u.Role, UserRoles.User) &
                     Builders<UserDocument>.Filter.In(u => u.FpId, nullableFpIds);

        return await _collection.Find(filter).ToListAsync();
    }
}
