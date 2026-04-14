using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

public class RecommendationRepository : IRecommendationRepository
{
    private readonly MongoDbContext _context;

    public RecommendationRepository(MongoDbContext context)
    {
        _context = context;
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

    public async Task<List<RecommendationDocument>> GetAllByUserIdAsync(Guid userId)
    {
        return await _context.Recommendations
            .Find(d => d.UserId == userId)
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
}
