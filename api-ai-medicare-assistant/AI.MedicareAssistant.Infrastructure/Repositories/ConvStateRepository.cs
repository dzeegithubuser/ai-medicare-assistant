using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

public class ConvStateRepository : IConvStateRepository
{
    private readonly MongoDbContext _context;

    public ConvStateRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<ConvStateDocument?> GetByUserIdAsync(Guid userId)
    {
        return await _context.ConvStates
            .Find(d => d.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(ConvStateDocument document)
    {
        var filter = Builders<ConvStateDocument>.Filter.Eq(d => d.UserId, document.UserId);
        var options = new ReplaceOptions { IsUpsert = true };
        await _context.ConvStates.ReplaceOneAsync(filter, document, options);
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        await _context.ConvStates.DeleteOneAsync(d => d.UserId == userId);
    }
}
