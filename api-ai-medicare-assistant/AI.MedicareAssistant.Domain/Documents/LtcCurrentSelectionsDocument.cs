using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

/// <summary>
/// MongoDB collection <c>ltcCurrentSelections</c>: one document per user holding their
/// current LTC care-type inputs and the last projection result.
/// </summary>
public class LtcCurrentSelectionsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("userId")]
    public Guid UserId { get; set; }

    /// <summary>Fixed marker — one logical row per user.</summary>
    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("healthProfile")]
    public int HealthProfile { get; set; } = 1;

    [BsonElement("numberOfAdultDayHealthCareYears")]
    public int NumberOfAdultDayHealthCareYears { get; set; }

    [BsonElement("numberOfHomeCareYears")]
    public int NumberOfHomeCareYears { get; set; }

    [BsonElement("numberOfNursingCareYears")]
    public int NumberOfNursingCareYears { get; set; }

    /// <summary>Serialised JSON of the last LTC API projection response.</summary>
    [BsonElement("ltcResult")]
    public string? LtcResultJson { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
