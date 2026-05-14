using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

public class RecommendationRepository : IRecommendationRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<RecommendationRepository> _logger;

    public RecommendationRepository(MongoDbContext context, ILogger<RecommendationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RecommendationDocument?> GetByUserIdAsync(Guid userId)
    {
        return await _context.Recommendations
            .Find(d => d.UserId == userId && d.Status == "active")
            .SortByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<RecommendationDocument?> GetByIdAsync(string id, Guid userId)
    {
        return await _context.Recommendations
            .Find(d => d.Id == id && d.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<RecommendationDocument?> GetByIdAsync(string id)
    {
        return await _context.Recommendations
            .Find(d => d.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<RecommendationDocument>> GetAllByUserIdAsync(Guid userId)
    {
        return await _context.Recommendations
            .Find(d => d.UserId == userId)
            .SortByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<RecommendationDocument>> GetByUserIdsAsync(IEnumerable<Guid> userIds)
    {
        var idList = userIds as IList<Guid> ?? userIds.ToList();
        if (idList.Count == 0) return new List<RecommendationDocument>();

        var filter = Builders<RecommendationDocument>.Filter.In(d => d.UserId, idList);
        return await _context.Recommendations
            .Find(filter)
            .SortByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<RecommendationDocument> CreateAsync(RecommendationDocument document)
    {
        await _context.Recommendations.InsertOneAsync(document);
        return document;
    }

    public async Task ReplaceAsync(RecommendationDocument document)
    {
        document.UpdatedAt = DateTime.UtcNow;
        await _context.Recommendations.ReplaceOneAsync(
            d => d.Id == document.Id,
            document);
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId)
    {
        return await _context.Recommendations
            .Find(d => d.UserId == userId && d.Status == "active")
            .AnyAsync();
    }

    public async Task<bool> ExistsByNameAsync(Guid userId, string name)
    {
        return await _context.Recommendations
            .Find(d => d.UserId == userId && d.Name == name && d.Status == "active")
            .AnyAsync();
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        await _context.Recommendations.DeleteManyAsync(
            d => d.UserId == userId && d.Status == "active");
    }

    public async Task DeleteByIdAsync(string id)
    {
        await _context.Recommendations.DeleteOneAsync(d => d.Id == id);
    }
}
