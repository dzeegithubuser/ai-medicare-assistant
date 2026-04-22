using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

public class RecommendationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("status")]
    public string Status { get; set; } = "active";

    [BsonElement("type")]
    public string Type { get; set; } = "medicare";

    [BsonElement("profile")]
    public ProfileSnapshot Profile { get; set; } = new();

    [BsonElement("planSelections")]
    public List<SelectedPlanDoc> PlanSelections { get; set; } = [];

    [BsonElement("drugList")]
    public List<SelectedDrugDoc> DrugList { get; set; } = [];

    [BsonElement("pharmacy")]
    public SelectedPharmacyDoc? Pharmacy { get; set; }

    [BsonElement("mailOrderPharmacy")]
    public MailOrderPharmacyDoc? MailOrderPharmacy { get; set; }

    [BsonElement("lastCostSnapshot")]
    public CostSnapshotDoc? LastCostSnapshot { get; set; }

    [BsonElement("ltcSnapshot")]
    public LtcSnapshotDoc? LtcSnapshot { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ProfileSnapshot
{
    [BsonElement("recommendationName")]
    public string RecommendationName { get; set; } = "";

    [BsonElement("firstName")]
    public string FirstName { get; set; } = "";

    [BsonElement("lastName")]
    public string LastName { get; set; } = "";

    [BsonElement("dateOfBirth")]
    public DateOnly DateOfBirth { get; set; }

    [BsonElement("gender")]
    public string Gender { get; set; } = "";

    [BsonElement("zipCode")]
    public string ZipCode { get; set; } = "";

    [BsonElement("county")]
    public string County { get; set; } = "";

    [BsonElement("countyCode")]
    public string CountyCode { get; set; } = "";

    [BsonElement("state")]
    public string State { get; set; } = "";

    [BsonElement("city")]
    public string City { get; set; } = "";

    [BsonElement("addressLine1")]
    public string AddressLine1 { get; set; } = "";

    [BsonElement("healthCondition")]
    public int HealthCondition { get; set; } = 1;

    [BsonElement("lifeExpectancy")]
    public int LifeExpectancy { get; set; } = 95;

    [BsonElement("tobaccoStatus")]
    public int TobaccoStatus { get; set; }

    [BsonElement("taxFilingStatus")]
    public string TaxFilingStatus { get; set; } = "";

    [BsonElement("magiTier")]
    public string MagiTier { get; set; } = "";

    [BsonElement("coverageYear")]
    public int CoverageYear { get; set; }

    [BsonElement("concierge")]
    public int Concierge { get; set; }

    [BsonElement("conciergeAmount")]
    public decimal? ConciergeAmount { get; set; }

    [BsonElement("alternateEmail")]
    public string? AlternateEmail { get; set; }

    [BsonElement("alternateMobile")]
    public string? AlternateMobile { get; set; }

    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    [BsonElement("longitude")]
    public double? Longitude { get; set; }
}

public class SelectedDrugDoc
{
    [BsonElement("drugName")]
    public string DrugName { get; set; } = "";

    [BsonElement("fullName")]
    public string? FullName { get; set; }

    [BsonElement("drugType")]
    public string? DrugType { get; set; }

    [BsonElement("dosage")]
    public string Dosage { get; set; } = "";

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("refillFrequency")]
    public string RefillFrequency { get; set; } = "";

    [BsonElement("rxcui")]
    public string? Rxcui { get; set; }

    [BsonElement("ndcCode")]
    public string? NdcCode { get; set; }
}

public class SelectedPlanDoc
{
    [BsonElement("planType")]
    public string PlanType { get; set; } = "";

    [BsonElement("planId")]
    public string PlanId { get; set; } = "";

    [BsonElement("planName")]
    public string PlanName { get; set; } = "";

    [BsonElement("carrier")]
    public string Carrier { get; set; } = "";

    [BsonElement("monthlyPremium")]
    public decimal MonthlyPremium { get; set; }

    [BsonElement("medigapPlanType")]
    public string? MedigapPlanType { get; set; }

    [BsonElement("deductible")]
    public decimal Deductible { get; set; }

    [BsonElement("starRating")]
    public double StarRating { get; set; }

    [BsonElement("totalPrescriptionCost")]
    public decimal TotalPrescriptionCost { get; set; }

    [BsonElement("totalPlanCost")]
    public decimal TotalPlanCost { get; set; }

    [BsonElement("prescriptionDrugCovered")]
    public bool PrescriptionDrugCovered { get; set; } = true;

    [BsonElement("unavailableDrugs")]
    public List<string> UnavailableDrugs { get; set; } = [];

    [BsonElement("planExpenses")]
    public List<PlanExpenseDoc> PlanExpenses { get; set; } = [];
}

public class SelectedPharmacyDoc
{
    [BsonElement("npi")]
    public string Npi { get; set; } = "";

    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("address")]
    public string Address { get; set; } = "";

    [BsonElement("city")]
    public string City { get; set; } = "";

    [BsonElement("state")]
    public string State { get; set; } = "";

    [BsonElement("zipCode")]
    public string ZipCode { get; set; } = "";

    [BsonElement("phone")]
    public string Phone { get; set; } = "";

    [BsonElement("pharmacyType")]
    public string PharmacyType { get; set; } = "";

    [BsonElement("distance")]
    public double? Distance { get; set; }
}

public class MailOrderPharmacyDoc
{
    [BsonElement("npi")]
    public string Npi { get; set; } = "";

    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("enabled")]
    public bool Enabled { get; set; }
}

public class CostSnapshotDoc
{
    [BsonElement("lifetimeTotal")]
    public decimal LifetimeTotal { get; set; }

    [BsonElement("lifetimePremiums")]
    public decimal LifetimePremiums { get; set; }

    [BsonElement("lifetimeOop")]
    public decimal LifetimeOop { get; set; }

    [BsonElement("lifetimeIrmaa")]
    public decimal LifetimeIrmaa { get; set; }

    [BsonElement("presentValue")]
    public decimal PresentValue { get; set; }

    [BsonElement("currentYearTotal")]
    public decimal CurrentYearTotal { get; set; }

    [BsonElement("calculatedAt")]
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("ltcPresentValue")]
    public decimal? LtcPresentValue { get; set; }

    [BsonElement("supplementPlanType")]
    public string SupplementPlanType { get; set; } = "";

    [BsonElement("supplementPlanPremium")]
    public decimal SupplementPlanPremium { get; set; }

    [BsonElement("yearlyDetails")]
    public List<YearlyDetailDoc> YearlyDetails { get; set; } = [];

    [BsonElement("evaluation")]
    public CostEvaluationDoc? Evaluation { get; set; }
}

public class YearlyDetailDoc
{
    [BsonElement("year")]
    public int Year { get; set; }

    [BsonElement("monthsUsedForExpenseCalc")]
    public int MonthsUsedForExpenseCalc { get; set; }

    [BsonElement("partAPremium")]
    public decimal PartAPremium { get; set; }

    [BsonElement("partBPremium")]
    public decimal PartBPremium { get; set; }

    [BsonElement("partBPremiumSurcharge")]
    public decimal PartBPremiumSurcharge { get; set; }

    [BsonElement("medicareAdvantagePremium")]
    public decimal MedicareAdvantagePremium { get; set; }

    [BsonElement("partDPremium")]
    public decimal PartDPremium { get; set; }

    [BsonElement("partDPremiumSurcharge")]
    public decimal PartDPremiumSurcharge { get; set; }

    [BsonElement("conciergePremium")]
    public decimal ConciergePremium { get; set; }

    [BsonElement("partAOOP")]
    public decimal PartAOOP { get; set; }

    [BsonElement("partBOOP")]
    public decimal PartBOOP { get; set; }

    [BsonElement("partDOOP")]
    public decimal PartDOOP { get; set; }

    [BsonElement("totalABMedicareAdvantage")]
    public decimal TotalABMedicareAdvantage { get; set; }

    [BsonElement("reserveDaysLeft")]
    public int ReserveDaysLeft { get; set; }

    [BsonElement("dentalPremium")]
    public decimal DentalPremium { get; set; }

    [BsonElement("dentalOOP")]
    public decimal DentalOOP { get; set; }

    [BsonElement("planGPremium")]
    public decimal PlanGPremium { get; set; }

    [BsonElement("planFPremium")]
    public decimal PlanFPremium { get; set; }

    [BsonElement("planNPremium")]
    public decimal PlanNPremium { get; set; }

    [BsonElement("totalABGD")]
    public decimal TotalABGD { get; set; }

    [BsonElement("totalABFD")]
    public decimal TotalABFD { get; set; }

    [BsonElement("totalABND")]
    public decimal TotalABND { get; set; }

    [BsonElement("totalABCD")]
    public decimal TotalABCD { get; set; }
}

public class CostEvaluationDoc
{
    [BsonElement("planName")]
    public string PlanName { get; set; } = "";

    [BsonElement("planBundleCode")]
    public string PlanBundleCode { get; set; } = "";

    [BsonElement("costTrajectory")]
    public string CostTrajectory { get; set; } = "";

    [BsonElement("trajectoryExplanation")]
    public string TrajectoryExplanation { get; set; } = "";

    [BsonElement("overallAssessment")]
    public string OverallAssessment { get; set; } = "";

    [BsonElement("lifetimeSummary")]
    public LifetimeSummaryDoc LifetimeSummary { get; set; } = new();

    [BsonElement("yearlyHighlights")]
    public List<YearlyHighlightDoc> YearlyHighlights { get; set; } = [];

    [BsonElement("categories")]
    public List<CostCategoryDoc> Categories { get; set; } = [];

    [BsonElement("savingsTips")]
    public List<SavingsTipDoc> SavingsTips { get; set; } = [];
}

public class LifetimeSummaryDoc
{
    [BsonElement("totalPremiums")]
    public decimal TotalPremiums { get; set; }

    [BsonElement("totalOutOfPocket")]
    public decimal TotalOutOfPocket { get; set; }

    [BsonElement("totalCombined")]
    public decimal TotalCombined { get; set; }

    [BsonElement("projectionYears")]
    public int ProjectionYears { get; set; }

    [BsonElement("averageAnnualCost")]
    public decimal AverageAnnualCost { get; set; }
}

public class YearlyHighlightDoc
{
    [BsonElement("year")]
    public int Year { get; set; }

    [BsonElement("totalCost")]
    public decimal TotalCost { get; set; }

    [BsonElement("flag")]
    public string Flag { get; set; } = "";

    [BsonElement("explanation")]
    public string Explanation { get; set; } = "";
}

public class CostCategoryDoc
{
    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("lifetimeTotal")]
    public decimal LifetimeTotal { get; set; }

    [BsonElement("percentOfTotal")]
    public double PercentOfTotal { get; set; }

    [BsonElement("trend")]
    public string Trend { get; set; } = "";

    [BsonElement("insight")]
    public string Insight { get; set; } = "";
}

public class SavingsTipDoc
{
    [BsonElement("title")]
    public string Title { get; set; } = "";

    [BsonElement("description")]
    public string Description { get; set; } = "";

    [BsonElement("estimatedSavings")]
    public string EstimatedSavings { get; set; } = "";

    [BsonElement("priority")]
    public string Priority { get; set; } = "";
}

public class PlanExpenseDoc
{
    [BsonElement("month")]
    public int Month { get; set; }

    [BsonElement("oop")]
    public decimal Oop { get; set; }

    [BsonElement("premium")]
    public decimal Premium { get; set; }

    [BsonElement("drugRetailCost")]
    public decimal DrugRetailCost { get; set; }
}

public class LtcSnapshotDoc
{
    [BsonElement("healthProfile")]
    public int HealthProfile { get; set; }

    [BsonElement("adultDayYears")]
    public int AdultDayYears { get; set; }

    [BsonElement("homeCareYears")]
    public int HomeCareYears { get; set; }

    [BsonElement("nursingCareYears")]
    public int NursingCareYears { get; set; }

    [BsonElement("totalCost")]
    public decimal TotalCost { get; set; }

    [BsonElement("totalPresentValue")]
    public decimal TotalPresentValue { get; set; }

    [BsonElement("projection")]
    public LtcProjectionDoc? Projection { get; set; }

    [BsonElement("evaluation")]
    public LtcEvaluationDoc? Evaluation { get; set; }
}

public class LtcProjectionDoc
{
    [BsonElement("pvHomeCare")]
    public decimal PvHomeCare { get; set; }

    [BsonElement("pvNursingCare")]
    public decimal PvNursingCare { get; set; }

    [BsonElement("adultDayExpenses")]
    public List<LtcExpenseEntryDoc> AdultDayExpenses { get; set; } = [];

    [BsonElement("homeCareExpenses")]
    public List<LtcExpenseEntryDoc> HomeCareExpenses { get; set; } = [];

    [BsonElement("assistedCareExpenses")]
    public List<LtcExpenseEntryDoc> AssistedCareExpenses { get; set; } = [];

    [BsonElement("nursingCareExpenses")]
    public List<LtcExpenseEntryDoc> NursingCareExpenses { get; set; } = [];
}

public class LtcExpenseEntryDoc
{
    [BsonElement("year")]
    public int Year { get; set; }

    [BsonElement("expense")]
    public decimal Expense { get; set; }
}

public class LtcEvaluationDoc
{
    [BsonElement("costTrajectory")]
    public string CostTrajectory { get; set; } = "";

    [BsonElement("trajectoryExplanation")]
    public string TrajectoryExplanation { get; set; } = "";

    [BsonElement("overallAssessment")]
    public string OverallAssessment { get; set; } = "";

    [BsonElement("totalCost")]
    public decimal TotalCost { get; set; }

    [BsonElement("totalPresentValue")]
    public decimal TotalPresentValue { get; set; }

    [BsonElement("projectionYears")]
    public int ProjectionYears { get; set; }

    [BsonElement("averageAnnualCost")]
    public decimal AverageAnnualCost { get; set; }

    [BsonElement("yearlyHighlights")]
    public List<YearlyHighlightDoc> YearlyHighlights { get; set; } = [];

    [BsonElement("categories")]
    public List<LtcCostCategoryDoc> Categories { get; set; } = [];

    [BsonElement("savingsTips")]
    public List<SavingsTipDoc> SavingsTips { get; set; } = [];
}

public class LtcCostCategoryDoc
{
    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("lifetimeTotal")]
    public decimal LifetimeTotal { get; set; }

    [BsonElement("presentValue")]
    public decimal PresentValue { get; set; }

    [BsonElement("percentOfTotal")]
    public double PercentOfTotal { get; set; }

    [BsonElement("trend")]
    public string Trend { get; set; } = "";

    [BsonElement("insight")]
    public string Insight { get; set; } = "";
}
