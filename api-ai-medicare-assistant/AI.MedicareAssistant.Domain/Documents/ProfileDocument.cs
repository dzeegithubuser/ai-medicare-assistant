using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

/// <summary>
/// MongoDB document for personal, medical, and address data captured through the profile screen.
/// Collection: <c>userProfiles</c>. Linked to <see cref="UserDocument"/> by <see cref="UserId"/>.
/// </summary>
[BsonIgnoreExtraElements]
public class ProfileDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>Foreign key to <see cref="UserDocument.UserId"/>.</summary>
    public Guid UserId { get; set; }

    // ── Health profile ──

    public int CoverageYear { get; set; }
    public int HealthCondition { get; set; } = 1;
    public string TaxFilingStatus { get; set; } = "MARRIED_FILING_JOINTLY";
    public string MagiTier { get; set; } = "";
    public string Gender { get; set; } = "F";
    public int TobaccoStatus { get; set; }
    public string? DateOfBirth { get; set; }
    public int LifeExpectancy { get; set; } = 95;

    // ── Concierge & alternate contact ──

    public int Concierge { get; set; }
    public decimal? ConciergeAmount { get; set; }
    public string? AlternateEmail { get; set; }
    public string? AlternateMobile { get; set; }

    // ── Address ──

    public string AddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string County { get; set; } = "";
    public string CountyCode { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>MongoDB document Id for the user's current analysis drugs.</summary>
    public string? CurrentPrescriptionDocumentId { get; set; }

    /// <summary>True once the profile section has been saved at least once.</summary>
    public bool IsProfileComplete { get; set; }

    // ── Audit ──

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string ModifiedBy { get; set; } = "system";
}
