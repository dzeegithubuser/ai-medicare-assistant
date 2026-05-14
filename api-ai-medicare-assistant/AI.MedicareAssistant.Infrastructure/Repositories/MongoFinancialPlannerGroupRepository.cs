using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

/// <summary>
/// MongoDB implementation of <see cref="IFinancialPlannerGroupRepository"/> against the
/// <c>financialPlannerGroups</c> collection.
/// </summary>
public class MongoFinancialPlannerGroupRepository : IFinancialPlannerGroupRepository
{
    private readonly IMongoCollection<FinancialPlannerGroupDocument> _collection;
    private readonly ILogger<MongoFinancialPlannerGroupRepository> _logger;

    public MongoFinancialPlannerGroupRepository(
        MongoDbContext context,
        ILogger<MongoFinancialPlannerGroupRepository> logger)
    {
        _collection = context.FinancialPlannerGroups;
        _logger = logger;
    }

    public async Task<FinancialPlannerGroupDocument?> GetByIdAsync(Guid groupId) =>
        await _collection.Find(g => g.GroupId == groupId).FirstOrDefaultAsync();

    public async Task<List<FinancialPlannerGroupDocument>> GetAllAsync() =>
        await _collection.Find(_ => true).ToListAsync();

    public async Task<FinancialPlannerGroupDocument> CreateAsync(FinancialPlannerGroupDocument doc)
    {
        await _collection.InsertOneAsync(doc);
        return doc;
    }

    public async Task UpdateAsync(FinancialPlannerGroupDocument doc)
    {
        doc.UpdatedAt = DateTime.UtcNow;

        if (string.IsNullOrEmpty(doc.Id))
        {
            var existingId = await _collection
                .Find(g => g.GroupId == doc.GroupId)
                .Project(g => g.Id)
                .FirstOrDefaultAsync();
            doc.Id = existingId ?? ObjectId.GenerateNewId().ToString();
        }

        await _collection.ReplaceOneAsync(g => g.GroupId == doc.GroupId, doc);
    }

    public async Task DeleteAsync(Guid groupId) =>
        await _collection.DeleteOneAsync(g => g.GroupId == groupId);

    public async Task<bool> ExistsByNameAsync(string name) =>
        await _collection.Find(g => g.Name == name).AnyAsync();
}
