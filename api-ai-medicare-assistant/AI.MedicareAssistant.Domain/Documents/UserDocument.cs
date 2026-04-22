using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

/// <summary>
/// MongoDB document combining the former MySQL User + Profile tables into a single document.
/// Collection: <c>users</c>.
/// </summary>
public class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>Application-level Guid kept for FK compatibility with other collections.</summary>
    public Guid UserId { get; set; } = Guid.NewGuid();

    // ── Auth fields (from User entity) ──

    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsEmailVerified { get; set; }

    // ── Profile fields (from Profile entity) ──

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int CoverageYear { get; set; }
    public int HealthCondition { get; set; } = 1;
    public string TaxFilingStatus { get; set; } = "MARRIED_FILING_JOINTLY";
    public string MagiTier { get; set; } = "";
    public string Gender { get; set; } = "F";
    public int TobaccoStatus { get; set; }
    public string? DateOfBirth { get; set; }
    public int Concierge { get; set; }
    public decimal? ConciergeAmount { get; set; }
    public string? AlternateEmail { get; set; }
    public string? AlternateMobile { get; set; }
    public int LifeExpectancy { get; set; } = 95;

    // Address
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

    // ── Audit fields ──

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public string ModifiedBy { get; set; } = "system";
}
