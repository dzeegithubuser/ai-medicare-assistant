using System.Text.Json.Serialization;
using Domain.Models.Pharmacy;

namespace Domain.Models;

public class DrugAnalysisResult
{
    [JsonPropertyName("drugs")]
    public List<DrugResult> Drugs { get; set; } = [];

    [JsonPropertyName("interactions")]
    public List<DrugInteraction> Interactions { get; set; } = [];

    [JsonPropertyName("dosageAlerts")]
    public List<DosageAlert> DosageAlerts { get; set; } = [];

    [JsonPropertyName("duplicateTherapies")]
    public List<DuplicateTherapy> DuplicateTherapies { get; set; } = [];

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }

    [JsonPropertyName("nearbyPharmacies")]
    public List<PharmacyWithPricing> NearbyPharmacies { get; set; } = [];

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class DrugResult
{
    [JsonPropertyName("drugInput")]
    public string DrugInput { get; set; } = "";

    [JsonPropertyName("normalizedDrugName")]
    public string NormalizedDrugName { get; set; } = "";

    [JsonPropertyName("brandNames")]
    public List<string> BrandNames { get; set; } = [];

    [JsonPropertyName("genericName")]
    public string GenericName { get; set; } = "";

    [JsonPropertyName("synonyms")]
    public List<string> Synonyms { get; set; } = [];

    [JsonPropertyName("therapeuticCategory")]
    public string TherapeuticCategory { get; set; } = "";

    [JsonPropertyName("drugClass")]
    public string DrugClass { get; set; } = "";

    [JsonPropertyName("mechanismOfAction")]
    public string MechanismOfAction { get; set; } = "";

    [JsonPropertyName("dosageForms")]
    public List<string> DosageForms { get; set; } = [];

    [JsonPropertyName("formulations")]
    public List<DrugFormulation> Formulations { get; set; } = [];

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = [];

    [JsonPropertyName("packaging")]
    public List<string> Packaging { get; set; } = [];

    [JsonPropertyName("rxNormId")]
    public string RxNormId { get; set; } = "";

    [JsonPropertyName("ndcCodes")]
    public List<string> NdcCodes { get; set; } = [];

    [JsonPropertyName("estimatedRetailCostUSD")]
    public string EstimatedRetailCostUSD { get; set; } = "";

    [JsonPropertyName("estimatedMedicarePartDCostUSD")]
    public string EstimatedMedicarePartDCostUSD { get; set; } = "";

    [JsonPropertyName("medicareNegotiatedPriceUSD")]
    public string MedicareNegotiatedPriceUSD { get; set; } = "";

    [JsonPropertyName("medicareVerificationLink")]
    public string MedicareVerificationLink { get; set; } = "";

    [JsonPropertyName("medicareCostEstimate")]
    public MedicareCostEstimate? MedicareCostEstimate { get; set; }

    [JsonPropertyName("confidenceScore")]
    public double? ConfidenceScore { get; set; }

    [JsonPropertyName("alternatives")]
    public List<DrugAlternative> Alternatives { get; set; } = [];

    [JsonPropertyName("genericSwitchSuggestion")]
    public GenericSwitchSuggestion? GenericSwitchSuggestion { get; set; }

    [JsonPropertyName("costBreakdown")]
    public MedicareCostBreakdown? CostBreakdown { get; set; }

    [JsonPropertyName("contraindications")]
    public List<string> Contraindications { get; set; } = [];
}

public class DrugFormulation
{
    [JsonPropertyName("dosageForm")]
    public string DosageForm { get; set; } = "";

    [JsonPropertyName("strength")]
    public string Strength { get; set; } = "";

    [JsonPropertyName("packaging")]
    public string Packaging { get; set; } = "";

    [JsonPropertyName("ndcCode")]
    public string NdcCode { get; set; } = "";
}

public class DrugInteraction
{
    [JsonPropertyName("drugA")]
    public string DrugA { get; set; } = "";

    [JsonPropertyName("drugB")]
    public string DrugB { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("clinicalConsequence")]
    public string ClinicalConsequence { get; set; } = "";

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = "";
}

public class DosageAlert
{
    [JsonPropertyName("drugName")]
    public string DrugName { get; set; } = "";

    [JsonPropertyName("inputDosage")]
    public string InputDosage { get; set; } = "";

    [JsonPropertyName("recommendedRange")]
    public string RecommendedRange { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class DuplicateTherapy
{
    [JsonPropertyName("drugs")]
    public List<string> Drugs { get; set; } = [];

    [JsonPropertyName("therapeuticClass")]
    public string TherapeuticClass { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class DrugAlternative
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("costDifference")]
    public string CostDifference { get; set; } = "";

    [JsonPropertyName("clinicalNote")]
    public string ClinicalNote { get; set; } = "";
}

public class GenericSwitchSuggestion
{
    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("to")]
    public string To { get; set; } = "";

    [JsonPropertyName("estimatedSavings")]
    public string EstimatedSavings { get; set; } = "";
}

public class MedicareCostBreakdown
{
    [JsonPropertyName("deductiblePhase")]
    public string DeductiblePhase { get; set; } = "";

    [JsonPropertyName("initialCoveragePhase")]
    public string InitialCoveragePhase { get; set; } = "";

    [JsonPropertyName("coverageGapPhase")]
    public string CoverageGapPhase { get; set; } = "";

    [JsonPropertyName("catastrophicPhase")]
    public string CatastrophicPhase { get; set; } = "";
}

public class MedicareCostEstimate
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "CMS Medicare Part D Spending Data";

    [JsonPropertyName("dataYear")]
    public string DataYear { get; set; } = "";

    [JsonPropertyName("drugName")]
    public string DrugName { get; set; } = "";

    [JsonPropertyName("totalClaims")]
    public int? TotalClaims { get; set; }

    [JsonPropertyName("totalBeneficiaries")]
    public int? TotalBeneficiaries { get; set; }

    [JsonPropertyName("averageCostPerClaim")]
    public decimal? AverageCostPerClaim { get; set; }

    [JsonPropertyName("averageMedicarePaymentPerClaim")]
    public decimal? AverageMedicarePaymentPerClaim { get; set; }

    [JsonPropertyName("averageBeneficiaryCostShare")]
    public decimal? AverageBeneficiaryCostShare { get; set; }

    [JsonPropertyName("totalSpending")]
    public decimal? TotalSpending { get; set; }

    [JsonPropertyName("averageSpendingPerBeneficiary")]
    public decimal? AverageSpendingPerBeneficiary { get; set; }
}
