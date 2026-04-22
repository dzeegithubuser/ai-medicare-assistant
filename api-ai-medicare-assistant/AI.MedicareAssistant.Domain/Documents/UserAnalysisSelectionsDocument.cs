using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

/// <summary>
/// MongoDB collection <c>userAnalysisSelections</c>: single current FP analysis row per user
/// (confirmed drugs, pharmacies, plans, section) — same treatment for all selections.
/// Named ad-hoc prescriptions without full analysis stay in <see cref="PrescriptionDocument"/>.
/// </summary>
public class UserAnalysisSelectionsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("userId")]
    public Guid UserId { get; set; }

    /// <summary>Same marker pattern — one logical row per user.</summary>
    [BsonElement("name")]
    public string Name { get; set; } = "";

    /// <summary>Confirmed drug rows (same shape as prescription drugs).</summary>
    [BsonElement("drugs")]
    public List<PrescriptionDrugDoc> Drugs { get; set; } = [];

    [BsonElement("fpActiveSection")]
    public string? FpActiveSection { get; set; }

    [BsonElement("selectedPharmacies")]
    public List<UserAnalysisPharmacyDoc> SelectedPharmacies { get; set; } = [];

    [BsonElement("selectedPlans")]
    public List<UserAnalysisPlanDoc> SelectedPlans { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class UserAnalysisPharmacyDoc
{
    [BsonElement("pharmacyNumber")]
    public string PharmacyNumber { get; set; } = "";

    [BsonElement("pharmacyName")]
    public string PharmacyName { get; set; } = "";

    [BsonElement("address")]
    public string Address { get; set; } = "";

    [BsonElement("distance")]
    public string Distance { get; set; } = "";

    [BsonElement("zipcode")]
    public string Zipcode { get; set; } = "";
}

public class UserAnalysisPlanDoc
{
    [BsonElement("slot")]
    public string Slot { get; set; } = "";

    [BsonElement("planId")]
    public string PlanId { get; set; } = "";

    [BsonElement("planName")]
    public string PlanName { get; set; } = "";

    [BsonElement("contractId")]
    public string ContractId { get; set; } = "";

    [BsonElement("medigapKey")]
    public string? MedigapKey { get; set; }

    [BsonElement("medigapPlanType")]
    public string? MedigapPlanType { get; set; }

    [BsonElement("companyName")]
    public string? CompanyName { get; set; }
}
