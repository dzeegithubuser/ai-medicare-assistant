using Domain.Documents;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infrastructure.Repositories;

public class ChatSessionRepository : IChatSessionRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<ChatSessionRepository> _logger;

    public ChatSessionRepository(MongoDbContext context, ILogger<ChatSessionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatSessionDocument?> GetByUserIdAsync(Guid userId)
    {
        try
        {
            return await _context.ChatSessions
                .Find(d => d.UserId == userId)
                .FirstOrDefaultAsync();
        }
        catch (BsonSerializationException ex)
        {
            _logger.LogWarning(ex,
                "Malformed chat-session document for UserId={UserId} — removing legacy docs",
                userId);
            await _context.ChatSessionsRaw.DeleteManyAsync(
                Builders<BsonDocument>.Filter.Eq("userId", userId));
            return null;
        }
    }

    public async Task UpsertAsync(ChatSessionDocument document)
    {
        var filter = Builders<ChatSessionDocument>.Filter.Eq(d => d.UserId, document.UserId);
        var options = new ReplaceOptions { IsUpsert = true };
        await _context.ChatSessions.ReplaceOneAsync(filter, document, options);
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        await _context.ChatSessions.DeleteManyAsync(d => d.UserId == userId);
    }
}
