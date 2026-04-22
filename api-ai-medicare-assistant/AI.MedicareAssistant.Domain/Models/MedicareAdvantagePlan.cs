namespace Domain.Models;

/// <summary>
/// Request for Medicare Advantage plan recommendations.
/// Same shape as PartDPlanRecommendationRequest + MedicareAdvantage = true.
/// </summary>
public class MedicareAdvantagePlanRequest
{
    public string UserId { get; set; } = "";
    public string SortRecommendations { get; set; } = "PREMIUM";
    public CountyCodeModel CountycodeModel { get; set; } = new();
    public List<PrescriptionInput> Prescriptions { get; set; } = [];
    public bool BeneficiaryCostDataRequired { get; set; }
    public bool PharmacyNetworkDataRequired { get; set; }
    public List<PharmacyInput> Pharmacies { get; set; } = [];
    public string PlanRecommendName { get; set; } = "";
    public string PlanRecommendEmail { get; set; } = "";
    public string DrugListingName { get; set; } = "";
    public string RecommendationListId { get; set; } = "";
    public string TaxFilingStatus { get; set; } = "";
    public int MagiTier { get; set; }
    public int HealthGrade { get; set; }
    public string BirthDate { get; set; } = "";
    public bool FullYearOOPCost { get; set; } = true;
    public string CoverageYear { get; set; } = "";
    public bool IncludePlanExpensesFullYear { get; set; } = true;
    public int PlanPage { get; set; } = 1;
    public int PlanPageSize { get; set; } = 12;
    public int RecommendationPage { get; set; } = 1;
    public int RecommendationPageSize { get; set; } = 12;
    public int? StarRatingFilter { get; set; }
    public string? PrescriptionCoverageFilter { get; set; }
    public string? ContractIdFilter { get; set; }
    public bool MailOrderPharmacy { get; set; }
    public int? RetirementYear { get; set; }

    /// <summary>Always true for Medicare Advantage requests.</summary>
    public bool MedicareAdvantage { get; set; } = true;
}
