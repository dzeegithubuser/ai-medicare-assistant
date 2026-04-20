using System.ComponentModel.DataAnnotations;
using Domain.Documents;

namespace Application.DTOs;

// ── Requests ──

public class CreateRecommendationRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public string Type { get; set; } = "medicare";

    [Required]
    public ProfileSnapshotDto Profile { get; set; } = new();

    public List<SelectedDrugDto> Drugs { get; set; } = [];
    public SelectedPharmacyDto? Pharmacy { get; set; }
    public List<SelectedPlanDto> Plans { get; set; } = [];
    public CostSnapshotDto? CostSnapshot { get; set; }
    public LtcSnapshotDto? LtcSnapshot { get; set; }
}

public class UpdateProfileRequest
{
    [Required]
    public ProfileSnapshotDto Profile { get; set; } = new();
}

public class UpdateDrugsRequest
{
    [Required]
    public List<SelectedDrugDto> Drugs { get; set; } = [];
}

public class UpdatePharmacyRequest
{
    public SelectedPharmacyDto? Pharmacy { get; set; }
    public MailOrderPharmacyDto? MailOrderPharmacy { get; set; }
}

public class UpdatePlansRequest
{
    [Required]
    public List<SelectedPlanDto> Plans { get; set; } = [];
}

// ── DTOs ──

public class ProfileSnapshotDto
{
    public string RecommendationName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string County { get; set; } = "";
    public string CountyCode { get; set; } = "";
    public string State { get; set; } = "";
    public string City { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public int HealthCondition { get; set; } = 1;
    public int LifeExpectancy { get; set; } = 95;
    public int TobaccoStatus { get; set; }
    public string TaxFilingStatus { get; set; } = "";
    public string MagiTier { get; set; } = "";
    public int CoverageYear { get; set; }
    public int Concierge { get; set; }
    public decimal? ConciergeAmount { get; set; }
    public string? AlternateEmail { get; set; }
    public string? AlternateMobile { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class SelectedDrugDto
{
    public string DrugName { get; set; } = "";
    public string? FullName { get; set; }
    public string? DrugType { get; set; }
    public string Dosage { get; set; } = "";
    public int Quantity { get; set; }
    public string RefillFrequency { get; set; } = "";
    public string? Rxcui { get; set; }
    public string? NdcCode { get; set; }
}

public class SelectedPlanDto
{
    public string PlanType { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string Carrier { get; set; } = "";
    public decimal MonthlyPremium { get; set; }
    public string? MedigapPlanType { get; set; }
    public decimal Deductible { get; set; }
    public double StarRating { get; set; }
    public decimal TotalPrescriptionCost { get; set; }
    public decimal TotalPlanCost { get; set; }
    public bool PrescriptionDrugCovered { get; set; } = true;
    public List<string> UnavailableDrugs { get; set; } = [];
    public List<PlanExpenseDto> PlanExpenses { get; set; } = [];
}

public class SelectedPharmacyDto
{
    public string Npi { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PharmacyType { get; set; } = "";
    public double? Distance { get; set; }
}

public class MailOrderPharmacyDto
{
    public string Npi { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
}

// ── Response ──

public class RecommendationResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Type { get; set; } = "medicare";
    public ProfileSnapshotDto Profile { get; set; } = new();
    public List<SelectedPlanDto> PlanSelections { get; set; } = [];
    public List<SelectedDrugDto> DrugList { get; set; } = [];
    public SelectedPharmacyDto? Pharmacy { get; set; }
    public MailOrderPharmacyDto? MailOrderPharmacy { get; set; }
    public CostSnapshotDto? LastCostSnapshot { get; set; }
    public LtcSnapshotDto? LtcSnapshot { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PlanSummaryItem
{
    public string PlanType { get; set; } = "";
    public string PlanName { get; set; } = "";
}

public class RecommendationSummaryResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Type { get; set; } = "medicare";
    public int DrugCount { get; set; }
    public int PlanCount { get; set; }
    public bool HasCostSnapshot { get; set; }
    public decimal LifetimeTotal { get; set; }
    public List<PlanSummaryItem> Plans { get; set; } = [];
    // LTC-specific summary fields
    public int? HealthProfile { get; set; }
    public int? AdultDayYears { get; set; }
    public int? HomeCareYears { get; set; }
    public int? NursingCareYears { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CostSnapshotDto
{
    public decimal LifetimeTotal { get; set; }
    public decimal LifetimePremiums { get; set; }
    public decimal LifetimeOop { get; set; }
    public decimal LifetimeIrmaa { get; set; }
    public decimal PresentValue { get; set; }
    public decimal CurrentYearTotal { get; set; }
    public DateTime CalculatedAt { get; set; }
    public decimal? LtcPresentValue { get; set; }
    public string SupplementPlanType { get; set; } = "";
    public decimal SupplementPlanPremium { get; set; }
    public List<YearlyDetailDto> YearlyDetails { get; set; } = [];
    public CostEvaluationDto? Evaluation { get; set; }
}

public class YearlyDetailDto
{
    public int Year { get; set; }
    public int MonthsUsedForExpenseCalc { get; set; }
    public decimal PartAPremium { get; set; }
    public decimal PartBPremium { get; set; }
    public decimal PartBPremiumSurcharge { get; set; }
    public decimal MedicareAdvantagePremium { get; set; }
    public decimal PartDPremium { get; set; }
    public decimal PartDPremiumSurcharge { get; set; }
    public decimal ConciergePremium { get; set; }
    public decimal PartAOOP { get; set; }
    public decimal PartBOOP { get; set; }
    public decimal PartDOOP { get; set; }
    public decimal TotalABMedicareAdvantage { get; set; }
    public int ReserveDaysLeft { get; set; }
    public decimal DentalPremium { get; set; }
    public decimal DentalOOP { get; set; }
    public decimal PlanGPremium { get; set; }
    public decimal PlanFPremium { get; set; }
    public decimal PlanNPremium { get; set; }
    public decimal TotalABGD { get; set; }
    public decimal TotalABFD { get; set; }
    public decimal TotalABND { get; set; }
    public decimal TotalABCD { get; set; }
}

public class CostEvaluationDto
{
    public string PlanName { get; set; } = "";
    public string PlanBundleCode { get; set; } = "";
    public string CostTrajectory { get; set; } = "";
    public string TrajectoryExplanation { get; set; } = "";
    public string OverallAssessment { get; set; } = "";
    public LifetimeSummaryDto LifetimeSummary { get; set; } = new();
    public List<YearlyHighlightDto> YearlyHighlights { get; set; } = [];
    public List<CostCategoryDto> Categories { get; set; } = [];
    public List<SavingsTipDto> SavingsTips { get; set; } = [];
}

public class LifetimeSummaryDto
{
    public decimal TotalPremiums { get; set; }
    public decimal TotalOutOfPocket { get; set; }
    public decimal TotalCombined { get; set; }
    public int ProjectionYears { get; set; }
    public decimal AverageAnnualCost { get; set; }
}

public class YearlyHighlightDto
{
    public int Year { get; set; }
    public decimal TotalCost { get; set; }
    public string Flag { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public class CostCategoryDto
{
    public string Name { get; set; } = "";
    public decimal LifetimeTotal { get; set; }
    public double PercentOfTotal { get; set; }
    public string Trend { get; set; } = "";
    public string Insight { get; set; } = "";
}

public class SavingsTipDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string EstimatedSavings { get; set; } = "";
    public string Priority { get; set; } = "";
}

public class PlanExpenseDto
{
    public int Month { get; set; }
    public decimal Oop { get; set; }
    public decimal Premium { get; set; }
    public decimal DrugRetailCost { get; set; }
}

// ── LTC Snapshot DTOs ──

public class LtcSnapshotDto
{
    public int HealthProfile { get; set; }
    public int AdultDayYears { get; set; }
    public int HomeCareYears { get; set; }
    public int NursingCareYears { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalPresentValue { get; set; }
    public LtcProjectionDto? Projection { get; set; }
    public LtcEvaluationDto? Evaluation { get; set; }
}

public class LtcProjectionDto
{
    public decimal PvHomeCare { get; set; }
    public decimal PvNursingCare { get; set; }
    public List<LtcExpenseEntryDto> AdultDayExpenses { get; set; } = [];
    public List<LtcExpenseEntryDto> HomeCareExpenses { get; set; } = [];
    public List<LtcExpenseEntryDto> AssistedCareExpenses { get; set; } = [];
    public List<LtcExpenseEntryDto> NursingCareExpenses { get; set; } = [];
}

public class LtcExpenseEntryDto
{
    public int Year { get; set; }
    public decimal Expense { get; set; }
}

public class LtcEvaluationDto
{
    public string CostTrajectory { get; set; } = "";
    public string TrajectoryExplanation { get; set; } = "";
    public string OverallAssessment { get; set; } = "";
    public decimal TotalCost { get; set; }
    public decimal TotalPresentValue { get; set; }
    public int ProjectionYears { get; set; }
    public decimal AverageAnnualCost { get; set; }
    public List<YearlyHighlightDto> YearlyHighlights { get; set; } = [];
    public List<LtcCostCategoryDto> Categories { get; set; } = [];
    public List<SavingsTipDto> SavingsTips { get; set; } = [];
}

public class LtcCostCategoryDto
{
    public string Name { get; set; } = "";
    public decimal LifetimeTotal { get; set; }
    public decimal PresentValue { get; set; }
    public double PercentOfTotal { get; set; }
    public string Trend { get; set; } = "";
    public string Insight { get; set; } = "";
}
