using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Data;

/// <summary>
/// One-shot migration that splits the legacy unified <c>users</c> document into
/// <c>users</c> (login/identity) + <c>userProfiles</c> (personal/medical/address).
///
/// Idempotent: a marker document in the <c>schemaMigrations</c> collection records completion.
/// Field names below are camelCase to match the CamelCaseElementNameConvention applied globally.
/// </summary>
public class UserProfileSplitMigrationInitializer : IHostedService
{
    private const string MigrationId = "split-userdocument-2026-05-13";
    private const string MarkerCollection = "schemaMigrations";

    private static readonly string[] ProfileFields =
    {
        "coverageYear", "healthCondition", "taxFilingStatus", "magiTier",
        "gender", "tobaccoStatus", "dateOfBirth", "lifeExpectancy",
        "concierge", "conciergeAmount", "alternateEmail", "alternateMobile",
        "addressLine1", "city", "state", "zipCode", "county", "countyCode",
        "latitude", "longitude",
        "currentPrescriptionDocumentId", "isProfileComplete"
    };

    private readonly IMongoDatabase _database;
    private readonly ILogger<UserProfileSplitMigrationInitializer> _logger;

    public UserProfileSplitMigrationInitializer(
        IMongoDatabase database,
        ILogger<UserProfileSplitMigrationInitializer> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var markers = _database.GetCollection<BsonDocument>(MarkerCollection);
        var alreadyRun = await markers.Find(Builders<BsonDocument>.Filter.Eq("_id", MigrationId))
            .AnyAsync(cancellationToken);

        if (alreadyRun)
        {
            _logger.LogInformation("User/profile split migration already applied; skipping.");
            return;
        }

        var users = _database.GetCollection<BsonDocument>("users");
        var profiles = _database.GetCollection<BsonDocument>("userProfiles");

        var cursor = await users.Find(Builders<BsonDocument>.Filter.Empty)
            .ToCursorAsync(cancellationToken);

        var migrated = 0;
        var skipped = 0;

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var legacy in cursor.Current)
            {
                if (!legacy.TryGetValue("userId", out var userIdVal))
                {
                    skipped++;
                    continue;
                }

                var carriesProfileData = ProfileFields.Any(legacy.Contains);
                if (carriesProfileData)
                {
                    var profile = new BsonDocument
                    {
                        ["_id"] = ObjectId.GenerateNewId(),
                        ["userId"] = userIdVal,
                        ["coverageYear"] = legacy.GetValue("coverageYear", 0),
                        ["healthCondition"] = legacy.GetValue("healthCondition", 1),
                        ["taxFilingStatus"] = legacy.GetValue("taxFilingStatus", "MARRIED_FILING_JOINTLY"),
                        ["magiTier"] = legacy.GetValue("magiTier", ""),
                        ["gender"] = legacy.GetValue("gender", "F"),
                        ["tobaccoStatus"] = legacy.GetValue("tobaccoStatus", 0),
                        ["dateOfBirth"] = legacy.GetValue("dateOfBirth", BsonNull.Value),
                        ["lifeExpectancy"] = legacy.GetValue("lifeExpectancy", 95),
                        ["concierge"] = legacy.GetValue("concierge", 0),
                        ["conciergeAmount"] = legacy.GetValue("conciergeAmount", BsonNull.Value),
                        ["alternateEmail"] = legacy.GetValue("alternateEmail", BsonNull.Value),
                        ["alternateMobile"] = legacy.GetValue("alternateMobile", BsonNull.Value),
                        ["addressLine1"] = legacy.GetValue("addressLine1", ""),
                        ["city"] = legacy.GetValue("city", ""),
                        ["state"] = legacy.GetValue("state", ""),
                        ["zipCode"] = legacy.GetValue("zipCode", ""),
                        ["county"] = legacy.GetValue("county", ""),
                        ["countyCode"] = legacy.GetValue("countyCode", ""),
                        ["latitude"] = legacy.GetValue("latitude", BsonNull.Value),
                        ["longitude"] = legacy.GetValue("longitude", BsonNull.Value),
                        ["currentPrescriptionDocumentId"] = legacy.GetValue("currentPrescriptionDocumentId", BsonNull.Value),
                        ["isProfileComplete"] = legacy.GetValue("isProfileComplete", false),
                        ["createdAt"] = legacy.GetValue("createdAt", DateTime.UtcNow),
                        ["updatedAt"] = legacy.GetValue("updatedAt", DateTime.UtcNow),
                        ["modifiedBy"] = legacy.GetValue("modifiedBy", "migration")
                    };

                    await profiles.ReplaceOneAsync(
                        Builders<BsonDocument>.Filter.Eq("userId", userIdVal),
                        profile,
                        new ReplaceOptions { IsUpsert = true },
                        cancellationToken);

                    migrated++;
                }

                var unset = Builders<BsonDocument>.Update.Combine(
                    ProfileFields.Select(f => Builders<BsonDocument>.Update.Unset(f)));
                await users.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", legacy["_id"]),
                    unset,
                    cancellationToken: cancellationToken);
            }
        }

        await markers.InsertOneAsync(new BsonDocument
        {
            ["_id"] = MigrationId,
            ["appliedAt"] = DateTime.UtcNow,
            ["migrated"] = migrated,
            ["skipped"] = skipped
        }, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "User/profile split migration completed: migrated={Migrated} skipped={Skipped}", migrated, skipped);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
