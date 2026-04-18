using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

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
