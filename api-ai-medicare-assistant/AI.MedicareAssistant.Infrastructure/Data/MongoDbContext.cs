using Domain.Documents;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Data;

/// <summary>
/// Provides typed access to MongoDB collections for the Medicare Assistant.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
    }

    /// <summary>Current FP pharmacy + plan selections per user (not drug rows).</summary>
    public IMongoCollection<UserAnalysisSelectionsDocument> UserAnalysisSelections
        => _database.GetCollection<UserAnalysisSelectionsDocument>("userAnalysisSelections");

    public IMongoCollection<RecommendationDocument> Recommendations
        => _database.GetCollection<RecommendationDocument>("recommendations");

    public IMongoCollection<ChatSessionDocument> ChatSessions
        => _database.GetCollection<ChatSessionDocument>("chatSessions");

    public IMongoCollection<BsonDocument> ChatSessionsRaw
        => _database.GetCollection<BsonDocument>("chatSessions");

    /// <summary>Current LTC care-type selections + last projection result per user.</summary>
    public IMongoCollection<LtcCurrentSelectionsDocument> LtcCurrentSelections
        => _database.GetCollection<LtcCurrentSelectionsDocument>("ltcCurrentSelections");

    /// <summary>Users (auth + profile combined).</summary>
    public IMongoCollection<UserDocument> Users
        => _database.GetCollection<UserDocument>("users");
}

/// <summary>
/// Creates MongoDB indexes at application startup without blocking the constructor.
/// Registered as an <see cref="IHostedService"/> so the work runs asynchronously.
/// </summary>
public class MongoIndexInitializer : IHostedService
{
    private readonly MongoDbContext _context;

    public MongoIndexInitializer(MongoDbContext context)
    {
        _context = context;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _context.UserAnalysisSelections.Indexes.CreateOneAsync(
            new CreateIndexModel<UserAnalysisSelectionsDocument>(
                Builders<UserAnalysisSelectionsDocument>.IndexKeys
                    .Ascending(d => d.UserId)
                    .Ascending(d => d.Name),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await _context.Recommendations.Indexes.CreateOneAsync(
            new CreateIndexModel<RecommendationDocument>(
                Builders<RecommendationDocument>.IndexKeys
                    .Ascending(d => d.UserId)
                    .Ascending(d => d.Status)),
            cancellationToken: cancellationToken);

        await _context.Recommendations.Indexes.CreateOneAsync(
            new CreateIndexModel<RecommendationDocument>(
                Builders<RecommendationDocument>.IndexKeys.Descending(d => d.CreatedAt)),
            cancellationToken: cancellationToken);

        await _context.ChatSessions.Indexes.CreateOneAsync(
            new CreateIndexModel<ChatSessionDocument>(
                Builders<ChatSessionDocument>.IndexKeys.Ascending(d => d.UserId),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await _context.ChatSessions.Indexes.CreateOneAsync(
            new CreateIndexModel<ChatSessionDocument>(
                Builders<ChatSessionDocument>.IndexKeys
                    .Ascending(d => d.UserId)
                    .Descending(d => d.UpdatedAt)),
            cancellationToken: cancellationToken);

        await _context.LtcCurrentSelections.Indexes.CreateOneAsync(
            new CreateIndexModel<LtcCurrentSelectionsDocument>(
                Builders<LtcCurrentSelectionsDocument>.IndexKeys
                    .Ascending(d => d.UserId)
                    .Ascending(d => d.Name),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        // Users — unique Email, unique Phone, unique UserId
        await _context.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(d => d.Email),
                new CreateIndexOptions { Unique = true, Sparse = true }),
            cancellationToken: cancellationToken);

        await _context.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(d => d.Phone),
                new CreateIndexOptions { Unique = true, Sparse = true }),
            cancellationToken: cancellationToken);

        await _context.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(d => d.UserId),
                new CreateIndexOptions { Unique = true, Sparse = true }),
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
