using System.Text.Json.Serialization;

namespace Domain.Models;

public enum LisTier { None, Partial, Full }
public enum PlanType { MedicareAdvantage, PartDPlusMedigap, DSNPDualEligible }

public record PlanRecommendationRequest(
    string UserId,
    string ZipCode,
    string? County,
    List<string> RxCuis,
    string MagiTier,
    decimal AnnualIncome,
    int HouseholdSize,
    string IncomeFilingStatus,
    bool HasEmployerCoverage,
    string? DisabilityStatus,
    bool HasChronicCondition,
    string? ChronicConditionDetails,
    int? RetirementAge,
    int? Age,
    List<DrugSummary> DrugSummaries,
    List<SelectedPharmacy>? SelectedPharmacies = null
);

/// <summary>
/// Pharmacy selected by the user in step 3 of the recommendation flow.
/// </summary>
public record SelectedPharmacy(
    string Npi,
    string Name,
    string PharmacyType
);

/// <summary>
/// Lightweight drug info passed into the plan recommendation pipeline.
/// </summary>
public record DrugSummary(
    string RxCui,
    string DrugName,
    string GenericName,
    string? SelectedName,
    string? NameType,
    string? TherapeuticCategory,
    string? DrugClass,
    string? DosageForm,
    string? Strength,
    string? Packaging,
    string? Ndc
);

public class PlanRecommendationResult
{
    [JsonPropertyName("lisEligible")]
    public bool LisEligible { get; set; }

    [JsonPropertyName("lisTier")]
    public string LisTier { get; set; } = "None";

    [JsonPropertyName("recommendedPlanType")]
    public string RecommendedPlanType { get; set; } = "";

    [JsonPropertyName("eligibilitySummary")]
    public string EligibilitySummary { get; set; } = "";

    [JsonPropertyName("lisCallToAction")]
    public string? LisCallToAction { get; set; }

    [JsonPropertyName("rankedPlans")]
    public List<RankedPlan> RankedPlans { get; set; } = [];
}

public class RankedPlan
{
    [JsonPropertyName("planId")]
    public string PlanId { get; set; } = "";

    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = "";

    [JsonPropertyName("planType")]
    public string PlanType { get; set; } = "";

    [JsonPropertyName("planCategory")]
    public string PlanCategory { get; set; } = "";

    [JsonPropertyName("insuranceName")]
    public string InsuranceName { get; set; } = "";

    [JsonPropertyName("monthlyPremium")]
    public decimal MonthlyPremium { get; set; }

    [JsonPropertyName("annualDeductible")]
    public decimal AnnualDeductible { get; set; }

    [JsonPropertyName("annualMoop")]
    public decimal AnnualMoop { get; set; }

    [JsonPropertyName("estimatedAnnualDrugCost")]
    public decimal EstimatedAnnualDrugCost { get; set; }

    [JsonPropertyName("estimatedAnnualTotalCost")]
    public decimal EstimatedAnnualTotalCost { get; set; }

    [JsonPropertyName("drugCoverages")]
    public List<PlanDrugCoverage> DrugCoverages { get; set; } = [];

    [JsonPropertyName("aiExplanation")]
    public string AiExplanation { get; set; } = "";

    [JsonPropertyName("starRating")]
    public string StarRating { get; set; } = "";

    [JsonPropertyName("hasPreferredPharmacyNetwork")]
    public bool HasPreferredPharmacyNetwork { get; set; }

    [JsonPropertyName("planFinderUrl")]
    public string PlanFinderUrl { get; set; } = "";

    // Extended plan benefit fields
    [JsonPropertyName("networkType")]
    public string NetworkType { get; set; } = "";

    [JsonPropertyName("includesDental")]
    public bool IncludesDental { get; set; }

    [JsonPropertyName("includesVision")]
    public bool IncludesVision { get; set; }

    [JsonPropertyName("includesHearing")]
    public bool IncludesHearing { get; set; }

    [JsonPropertyName("includesFitness")]
    public bool IncludesFitness { get; set; }

    [JsonPropertyName("includesOtc")]
    public bool IncludesOtc { get; set; }

    [JsonPropertyName("otcAllowancePerQuarter")]
    public decimal OtcAllowancePerQuarter { get; set; }

    [JsonPropertyName("gapCoverage")]
    public string GapCoverage { get; set; } = "None";

    [JsonPropertyName("mailOrderSavings")]
    public bool MailOrderSavings { get; set; }

    [JsonPropertyName("providerNetworkSize")]
    public string ProviderNetworkSize { get; set; } = "";

    [JsonPropertyName("emergencyCoverage")]
    public bool EmergencyCoverage { get; set; }

    [JsonPropertyName("pros")]
    public List<string> Pros { get; set; } = [];

    [JsonPropertyName("cons")]
    public List<string> Cons { get; set; } = [];

    [JsonPropertyName("costBreakdowns")]
    public List<PlanCostBreakdown>? CostBreakdowns { get; set; }
}

/// <summary>
/// Detailed cost breakdown for a plan at the user's selected pharmacy.
/// </summary>
public class PlanCostBreakdown
{
    [JsonPropertyName("pharmacyName")]
    public string PharmacyName { get; set; } = "";

    [JsonPropertyName("pharmacyNpi")]
    public string PharmacyNpi { get; set; } = "";

    [JsonPropertyName("isPreferredPharmacy")]
    public bool IsPreferredPharmacy { get; set; }

    [JsonPropertyName("annualPremium")]
    public decimal AnnualPremium { get; set; }

    [JsonPropertyName("annualDeductible")]
    public decimal AnnualDeductible { get; set; }

    [JsonPropertyName("annualDrugCopay")]
    public decimal AnnualDrugCopay { get; set; }

    [JsonPropertyName("annualTotal")]
    public decimal AnnualTotal { get; set; }

    [JsonPropertyName("drugCopays")]
    public List<DrugCopayDetail> DrugCopays { get; set; } = [];
}

public class DrugCopayDetail
{
    [JsonPropertyName("drugName")]
    public string DrugName { get; set; } = "";

    [JsonPropertyName("rxCui")]
    public string RxCui { get; set; } = "";

    [JsonPropertyName("formularyTier")]
    public int FormularyTier { get; set; }

    [JsonPropertyName("monthlyCopay")]
    public decimal MonthlyCopay { get; set; }

    [JsonPropertyName("annualCopay")]
    public decimal AnnualCopay { get; set; }

    [JsonPropertyName("isCovered")]
    public bool IsCovered { get; set; }

    [JsonPropertyName("preferredDiscount")]
    public bool PreferredDiscount { get; set; }
}

public class PlanDrugCoverage
{
    [JsonPropertyName("drugName")]
    public string DrugName { get; set; } = "";

    [JsonPropertyName("rxCui")]
    public string RxCui { get; set; } = "";

    [JsonPropertyName("isCovered")]
    public bool IsCovered { get; set; }

    [JsonPropertyName("formularyTier")]
    public int FormularyTier { get; set; }

    [JsonPropertyName("monthlyCopay")]
    public decimal MonthlyCopay { get; set; }

    [JsonPropertyName("requiresPriorAuth")]
    public bool RequiresPriorAuth { get; set; }

    [JsonPropertyName("hasQuantityLimit")]
    public bool HasQuantityLimit { get; set; }

    [JsonPropertyName("quantityLimitDetail")]
    public string? QuantityLimitDetail { get; set; }
}

// ── CMS Open Data models (Phase 2) ──

/// <summary>
/// Real plan data from CMS SOCRATA datasets — used to enrich AI-generated recommendations.
/// </summary>
public class CmsPlanInfo
{
    public string ContractId { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string PlanType { get; set; } = "";
    public decimal MonthlyPremium { get; set; }
    public decimal AnnualDeductible { get; set; }
    public decimal StarRating { get; set; }
    public string StateCode { get; set; } = "";
    public string CountyName { get; set; } = "";
}

/// <summary>
/// Formulary tier/coverage data for a specific drug within a plan.
/// </summary>
public class CmsFormularyEntry
{
    public string RxCui { get; set; } = "";
    public string DrugName { get; set; } = "";
    public string ContractId { get; set; } = "";
    public int FormularyTier { get; set; }
    public bool RequiresPriorAuth { get; set; }
    public bool HasQuantityLimit { get; set; }
    public bool HasStepTherapy { get; set; }
}
