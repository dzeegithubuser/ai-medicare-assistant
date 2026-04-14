using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

public class ConvStateDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("state")]
    [BsonRepresentation(BsonType.String)]
    public ConversationState State { get; set; } = ConversationState.Idle;

    [BsonElement("activeIntent")]
    public string? ActiveIntent { get; set; }

    [BsonElement("pendingChanges")]
    public BsonDocument PendingChanges { get; set; } = new();

    [BsonElement("collectedFields")]
    public BsonDocument CollectedFields { get; set; } = new();

    [BsonElement("awaitingConfirmationFor")]
    public string? AwaitingConfirmationFor { get; set; }

    [BsonElement("lastActivity")]
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
}

public enum ConversationState
{
    Idle,
    CollectingProfile,
    CollectingDrugs,
    CollectingPharmacy,
    CollectingPlans,
    AwaitingConfirmation,
    AwaitingDeletePhrase,
    ShowingComparison,
    ShowingProjections,
    WhatIfMode
}
