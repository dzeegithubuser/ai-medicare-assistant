using Domain.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    /// <summary>Users — login and identity.</summary>
    public IMongoCollection<UserDocument> Users
        => _database.GetCollection<UserDocument>("users");

    /// <summary>Raw users collection for the one-shot login/profile split migration.</summary>
    public IMongoCollection<BsonDocument> UsersRaw
        => _database.GetCollection<BsonDocument>("users");

    /// <summary>User profiles — personal, medical, and address data.</summary>
    public IMongoCollection<ProfileDocument> UserProfiles
        => _database.GetCollection<ProfileDocument>("userProfiles");

    /// <summary>Financial Planner Groups — tenants owning one or more FP users.</summary>
    public IMongoCollection<FinancialPlannerGroupDocument> FinancialPlannerGroups
        => _database.GetCollection<FinancialPlannerGroupDocument>("financialPlannerGroups");
}

/// <summary>
/// Creates MongoDB indexes at application startup without blocking the constructor.
/// Registered as an <see cref="IHostedService"/> so the work runs asynchronously.
/// </summary>
public class MongoIndexInitializer : IHostedService
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoIndexInitializer>? _logger;

    public MongoIndexInitializer(MongoDbContext context, ILogger<MongoIndexInitializer>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await DropLegacyPascalCaseIndexesAsync(cancellationToken);
        await DropOptionDriftedIndexesAsync(cancellationToken);

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
                new CreateIndexOptions { Unique = true, Sparse = true }),
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

        // Users — role-hierarchy lookup indexes (non-unique)
        await _context.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(d => d.FpId)),
            cancellationToken: cancellationToken);

        await _context.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(d => d.FpgId)),
            cancellationToken: cancellationToken);

        await _context.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(d => d.Role)),
            cancellationToken: cancellationToken);

        // UserProfiles — unique UserId
        await _context.UserProfiles.Indexes.CreateOneAsync(
            new CreateIndexModel<ProfileDocument>(
                Builders<ProfileDocument>.IndexKeys.Ascending(d => d.UserId),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        // FinancialPlannerGroups — unique GroupId, unique Name
        await _context.FinancialPlannerGroups.Indexes.CreateOneAsync(
            new CreateIndexModel<FinancialPlannerGroupDocument>(
                Builders<FinancialPlannerGroupDocument>.IndexKeys.Ascending(d => d.GroupId),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await _context.FinancialPlannerGroups.Indexes.CreateOneAsync(
            new CreateIndexModel<FinancialPlannerGroupDocument>(
                Builders<FinancialPlannerGroupDocument>.IndexKeys.Ascending(d => d.Name),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Drops indexes whose name starts with an uppercase letter on collections we own.
    /// These are stale PascalCase-keyed indexes left over from before the
    /// <c>CamelCaseElementNameConvention</c> was applied; they key on field names no
    /// current document has, so every doc indexes as <c>null</c> there and the unique
    /// constraint fires on the second insert.
    ///
    /// The check is "starts with uppercase" — not "contains uppercase" — because correct
    /// camelCase index names embed inner capitals (e.g. <c>userId_1</c>, <c>updatedAt_-1</c>)
    /// and we must keep those.
    ///
    /// Safe to run on every startup: dropping a non-existent index is a no-op. Skips
    /// <c>_id_</c> (always present) and any index whose first key segment is lowercase.
    /// </summary>
    private async Task DropLegacyPascalCaseIndexesAsync(CancellationToken cancellationToken)
    {
        string[] collections =
        {
            "chatSessions", "users", "userProfiles", "recommendations",
            "userAnalysisSelections", "ltcCurrentSelections", "financialPlannerGroups"
        };

        foreach (var name in collections)
        {
            try
            {
                var collection = _context.UsersRaw.Database.GetCollection<BsonDocument>(name);
                using var cursor = await collection.Indexes.ListAsync(cancellationToken);
                var indexes = await cursor.ToListAsync(cancellationToken);

                foreach (var idx in indexes)
                {
                    var indexName = idx["name"].AsString;
                    if (indexName == "_id_") continue;
                    if (indexName.Length == 0 || !char.IsUpper(indexName[0])) continue;

                    try
                    {
                        await collection.Indexes.DropOneAsync(indexName, cancellationToken);
                        _logger?.LogInformation(
                            "Dropped legacy PascalCase index {IndexName} on {Collection}", indexName, name);
                    }
                    catch (MongoCommandException ex)
                    {
                        _logger?.LogWarning(ex,
                            "Could not drop legacy index {IndexName} on {Collection}", indexName, name);
                    }
                }
            }
            catch (MongoCommandException ex) when (ex.CodeName == "NamespaceNotFound")
            {
                // Collection doesn't exist yet — nothing to clean up.
            }
        }
    }

    /// <summary>
    /// Drops indexes whose options have drifted from what <see cref="StartAsync"/> will
    /// re-create. MongoDB refuses <c>createIndexes</c> when an index with the same name +
    /// keys but different options (e.g. <c>sparse</c>) already exists — so the only path
    /// to change options is drop-and-recreate.
    ///
    /// Currently handles a single known drift: <c>chatSessions.userId_1</c> existed as
    /// <c>{unique:true}</c> in earlier deployments; it is now <c>{unique:true, sparse:true}</c>.
    /// </summary>
    private async Task DropOptionDriftedIndexesAsync(CancellationToken cancellationToken)
    {
        var drifts = new (string Collection, string IndexName, bool ShouldBeSparse)[]
        {
            ("chatSessions", "userId_1", true),
        };

        foreach (var (collectionName, indexName, shouldBeSparse) in drifts)
        {
            try
            {
                var collection = _context.UsersRaw.Database.GetCollection<BsonDocument>(collectionName);
                using var cursor = await collection.Indexes.ListAsync(cancellationToken);
                var indexes = await cursor.ToListAsync(cancellationToken);

                var existing = indexes.FirstOrDefault(i => i["name"].AsString == indexName);
                if (existing is null) continue;

                var existingSparse = existing.GetValue("sparse", false).ToBoolean();
                if (existingSparse == shouldBeSparse) continue;

                try
                {
                    await collection.Indexes.DropOneAsync(indexName, cancellationToken);
                    _logger?.LogInformation(
                        "Dropped option-drifted index {IndexName} on {Collection} (sparse {Was}→{Now})",
                        indexName, collectionName, existingSparse, shouldBeSparse);
                }
                catch (MongoCommandException ex)
                {
                    _logger?.LogWarning(ex,
                        "Could not drop drifted index {IndexName} on {Collection}", indexName, collectionName);
                }
            }
            catch (MongoCommandException ex) when (ex.CodeName == "NamespaceNotFound")
            {
                // Collection doesn't exist yet — nothing to clean up.
            }
        }
    }
}
