using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

/// <summary>
/// MongoDB implementation of <see cref="IUserRepository"/> against the <c>users</c> collection.
/// </summary>
public class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _collection;

    public MongoUserRepository(MongoDbContext context) =>
        _collection = context.Users;

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
        await _collection.ReplaceOneAsync(u => u.UserId == user.UserId, user);
    }

    public async Task<bool> EmailExistsAsync(string email) =>
        await _collection.Find(u => u.Email == email).AnyAsync();

    public async Task<bool> PhoneExistsAsync(string phone) =>
        await _collection.Find(u => u.Phone == phone).AnyAsync();
}
