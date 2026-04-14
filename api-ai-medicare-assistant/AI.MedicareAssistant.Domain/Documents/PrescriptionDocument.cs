using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

/// <summary>
/// MongoDB document representing a saved prescription with its drugs.
/// Replaces the MySQL Prescription + PrescriptionDrug tables.
/// </summary>
public class PrescriptionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("drugs")]
    public List<PrescriptionDrugDoc> Drugs { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PrescriptionDrugDoc
{
    [BsonElement("drugInput")]
    public string DrugInput { get; set; } = "";

    [BsonElement("normalizedDrugName")]
    public string NormalizedDrugName { get; set; } = "";

    [BsonElement("genericName")]
    public string GenericName { get; set; } = "";

    [BsonElement("selectedName")]
    public string SelectedName { get; set; } = "";

    [BsonElement("nameType")]
    public string NameType { get; set; } = "";

    [BsonElement("dosageForm")]
    public string DosageForm { get; set; } = "";

    [BsonElement("strength")]
    public string Strength { get; set; } = "";

    [BsonElement("packaging")]
    public string Packaging { get; set; } = "";

    [BsonElement("rxNormId")]
    public string RxNormId { get; set; } = "";

    [BsonElement("ndcCode")]
    public string NdcCode { get; set; } = "";

    [BsonElement("therapeuticCategory")]
    public string TherapeuticCategory { get; set; } = "";

    [BsonElement("drugClass")]
    public string DrugClass { get; set; } = "";

    /// <summary>Tablets/units per month from FP drug step.</summary>
    [BsonElement("quantityPerMonth")]
    public int? QuantityPerMonth { get; set; }
}
