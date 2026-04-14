using System.Text.Json.Serialization;

namespace Domain.Models;

/// <summary>
/// Request payload for the Financial Planner individualMedicareR5 API.
/// </summary>
public class IndividualMedicareRequest
{
    [JsonPropertyName("userEmail")]
    public string UserEmail { get; set; } = "";

    [JsonPropertyName("versionId")]
    public string VersionId { get; set; } = "3";

    [JsonPropertyName("birthDate")]
    public string BirthDate { get; set; } = "";

    [JsonPropertyName("retirementYear")]
    public string RetirementYear { get; set; } = "";

    [JsonPropertyName("lifeExpectancy")]
    public int LifeExpectancy { get; set; }

    [JsonPropertyName("singleVsMultipleMagi")]
    public string SingleVsMultipleMagi { get; set; } = "false";

    [JsonPropertyName("magiTierVsDollarAmount")]
    public string MagiTierVsDollarAmount { get; set; } = "false";

    [JsonPropertyName("healthGrade")]
    public int HealthGrade { get; set; }

    [JsonPropertyName("stateName")]
    public string StateName { get; set; } = "";

    [JsonPropertyName("zipcode")]
    public string Zipcode { get; set; } = "";

    [JsonPropertyName("retirementState")]
    public string RetirementState { get; set; } = "";

    [JsonPropertyName("retirementZipcode")]
    public string RetirementZipcode { get; set; } = "";

    [JsonPropertyName("boughtPlanA")]
    public bool BoughtPlanA { get; set; }

    [JsonPropertyName("reserveDaysUsed")]
    public int ReserveDaysUsed { get; set; }

    [JsonPropertyName("taxFilingStatus")]
    public string TaxFilingStatus { get; set; } = "";

    [JsonPropertyName("tobacco")]
    public int Tobacco { get; set; }

    [JsonPropertyName("magiTier")]
    public int MagiTier { get; set; }

    [JsonPropertyName("fullYearDataForLifeExpectancyYear")]
    public string FullYearDataForLifeExpectancyYear { get; set; } = "false";

    [JsonPropertyName("partDDataProvided")]
    public bool PartDDataProvided { get; set; }

    [JsonPropertyName("planRecommendName")]
    public string PlanRecommendName { get; set; } = "";

    [JsonPropertyName("planRecommendEmail")]
    public string PlanRecommendEmail { get; set; } = "";

    [JsonPropertyName("recommendationListId")]
    public string RecommendationListId { get; set; } = "";

    [JsonPropertyName("planBundleCode")]
    public string PlanBundleCode { get; set; } = "";

    [JsonPropertyName("supplementPlanDataProvided")]
    public bool SupplementPlanDataProvided { get; set; }

    [JsonPropertyName("supplementPlanType")]
    public string? SupplementPlanType { get; set; }

    [JsonPropertyName("medicareAdvantageDataProvided")]
    public bool MedicareAdvantageDataProvided { get; set; }

    [JsonPropertyName("coverageYear")]
    public string CoverageYear { get; set; } = "";

    [JsonPropertyName("conciergeIncluded")]
    public bool ConciergeIncluded { get; set; }

    [JsonPropertyName("conciergePremium")]
    public decimal ConciergePremium { get; set; }

    [JsonPropertyName("medicareAdvantagePremium")]
    public decimal MedicareAdvantagePremium { get; set; }

    [JsonPropertyName("maWithPrescriptionBenefit")]
    public bool MaWithPrescriptionBenefit { get; set; }

    [JsonPropertyName("partDOOP")]
    public decimal PartDOOP { get; set; }

    [JsonPropertyName("partDOOPFullYear")]
    public decimal PartDOOPFullYear { get; set; }

    [JsonPropertyName("partABenefitServiceCost")]
    public decimal PartABenefitServiceCost { get; set; }

    [JsonPropertyName("partBBenefitServiceCost")]
    public decimal PartBBenefitServiceCost { get; set; }

    [JsonPropertyName("calculateForAdjustedMonth")]
    public int CalculateForAdjustedMonth { get; set; }

    [JsonPropertyName("dental")]
    public bool Dental { get; set; }

    [JsonPropertyName("dentalHealthGrade")]
    public int DentalHealthGrade { get; set; }

    [JsonPropertyName("partDPremium")]
    public decimal PartDPremium { get; set; }
}

/// <summary>
/// Response from the Financial Planner individualMedicareR5 API.
/// </summary>
public class IndividualMedicareResponse
{
    [JsonPropertyName("webServiceTransactionId")]
    public string WebServiceTransactionId { get; set; } = "";

    [JsonPropertyName("webServiceStatus")]
    public string WebServiceStatus { get; set; } = "";

    [JsonPropertyName("birthDate")]
    public string BirthDate { get; set; } = "";

    [JsonPropertyName("retirementYear")]
    public string RetirementYear { get; set; } = "";

    [JsonPropertyName("singleVsMultipleMagi")]
    public bool SingleVsMultipleMagi { get; set; }

    [JsonPropertyName("magiTierVsDollarAmount")]
    public bool MagiTierVsDollarAmount { get; set; }

    [JsonPropertyName("lifeExpectancy")]
    public int LifeExpectancy { get; set; }

    [JsonPropertyName("magiTier")]
    public int MagiTier { get; set; }

    [JsonPropertyName("taxFilingStatus")]
    public string TaxFilingStatus { get; set; } = "";

    [JsonPropertyName("magiDollarAmount")]
    public decimal? MagiDollarAmount { get; set; }

    [JsonPropertyName("yearWiseMagiDetailList")]
    public List<object> YearWiseMagiDetailList { get; set; } = [];

    [JsonPropertyName("healthGrade")]
    public string HealthGrade { get; set; } = "";

    [JsonPropertyName("stateName")]
    public string StateName { get; set; } = "";

    [JsonPropertyName("zipcode")]
    public int Zipcode { get; set; }

    [JsonPropertyName("countyCode")]
    public string? CountyCode { get; set; }

    [JsonPropertyName("retirementState")]
    public string RetirementState { get; set; } = "";

    [JsonPropertyName("retirementZipcode")]
    public int RetirementZipcode { get; set; }

    [JsonPropertyName("retirementCountyCode")]
    public string? RetirementCountyCode { get; set; }

    [JsonPropertyName("versionId")]
    public int VersionId { get; set; }

    [JsonPropertyName("partDDataProvided")]
    public bool PartDDataProvided { get; set; }

    [JsonPropertyName("partDPremium")]
    public decimal PartDPremium { get; set; }

    [JsonPropertyName("partDOOP")]
    public decimal PartDOOP { get; set; }

    [JsonPropertyName("partDOOPFullYear")]
    public decimal PartDOOPFullYear { get; set; }

    [JsonPropertyName("medicareAdvantageDataProvided")]
    public bool MedicareAdvantageDataProvided { get; set; }

    [JsonPropertyName("medicareAdvantagePremium")]
    public decimal MedicareAdvantagePremium { get; set; }

    [JsonPropertyName("maPrescriptionOOP")]
    public decimal MaPrescriptionOOP { get; set; }

    [JsonPropertyName("supplementPlanDataProvided")]
    public bool SupplementPlanDataProvided { get; set; }

    [JsonPropertyName("supplementPlanType")]
    public string? SupplementPlanType { get; set; }

    [JsonPropertyName("supplementPlanPremium")]
    public decimal SupplementPlanPremium { get; set; }

    [JsonPropertyName("boughtPlanA")]
    public bool BoughtPlanA { get; set; }

    [JsonPropertyName("reserveDaysUsed")]
    public int ReserveDaysUsed { get; set; }

    [JsonPropertyName("dental")]
    public bool Dental { get; set; }

    [JsonPropertyName("dentalHealthGrade")]
    public int DentalHealthGrade { get; set; }

    [JsonPropertyName("tobacco")]
    public bool Tobacco { get; set; }

    [JsonPropertyName("fullYearDataForLifeExpectancyYear")]
    public bool FullYearDataForLifeExpectancyYear { get; set; }

    [JsonPropertyName("errorList")]
    public List<object> ErrorList { get; set; } = [];

    [JsonPropertyName("lifeTimeABMedicareAdvantageExpenses")]
    public decimal LifeTimeABMedicareAdvantageExpenses { get; set; }

    [JsonPropertyName("lifeTimeABMedicareAdvantagePremium")]
    public decimal LifeTimeABMedicareAdvantagePremium { get; set; }

    [JsonPropertyName("lifeTimeABMedicareAdvantageOop")]
    public decimal LifeTimeABMedicareAdvantageOop { get; set; }

    [JsonPropertyName("lifeTimeDSurcharge")]
    public decimal LifeTimeDSurcharge { get; set; }

    [JsonPropertyName("lifeTimeBSurcharge")]
    public decimal LifeTimeBSurcharge { get; set; }

    [JsonPropertyName("lifeTimeConciergePremium")]
    public decimal LifeTimeConciergePremium { get; set; }

    [JsonPropertyName("conciergeIncluded")]
    public bool ConciergeIncluded { get; set; }

    [JsonPropertyName("lifeTimeABGDExpenses")]
    public decimal LifeTimeABGDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABGDPremium")]
    public decimal LifeTimeABGDPremium { get; set; }

    [JsonPropertyName("lifeTimeABGDOop")]
    public decimal LifeTimeABGDOop { get; set; }

    [JsonPropertyName("lifeTimeABFDExpenses")]
    public decimal LifeTimeABFDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABFDPremium")]
    public decimal LifeTimeABFDPremium { get; set; }

    [JsonPropertyName("lifeTimeABFDOop")]
    public decimal LifeTimeABFDOop { get; set; }

    [JsonPropertyName("lifeTimeABNDExpenses")]
    public decimal LifeTimeABNDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABNDPremium")]
    public decimal LifeTimeABNDPremium { get; set; }

    [JsonPropertyName("lifeTimeABNDOop")]
    public decimal LifeTimeABNDOop { get; set; }

    [JsonPropertyName("lifeTimeABCDExpenses")]
    public decimal LifeTimeABCDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABCDPremium")]
    public decimal LifeTimeABCDPremium { get; set; }

    [JsonPropertyName("lifeTimeABCDOop")]
    public decimal LifeTimeABCDOop { get; set; }

    [JsonPropertyName("individualMedicares")]
    public List<IndividualMedicareDetail> IndividualMedicares { get; set; } = [];
}

/// <summary>
/// Year-by-year Medicare cost breakdown.
/// </summary>
public class IndividualMedicareDetail
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("monthsUsedForExpenseCalc")]
    public int MonthsUsedForExpenseCalc { get; set; }

    [JsonPropertyName("partAPremium")]
    public decimal PartAPremium { get; set; }

    [JsonPropertyName("partBPremium")]
    public decimal PartBPremium { get; set; }

    [JsonPropertyName("partBPremiumSurcharge")]
    public decimal PartBPremiumSurcharge { get; set; }

    [JsonPropertyName("medicareAdvantagePremium")]
    public decimal MedicareAdvantagePremium { get; set; }

    [JsonPropertyName("partDPremium")]
    public decimal PartDPremium { get; set; }

    [JsonPropertyName("partDPremiumSurcharge")]
    public decimal PartDPremiumSurcharge { get; set; }

    [JsonPropertyName("conciergePremium")]
    public decimal ConciergePremium { get; set; }

    [JsonPropertyName("partAOOP")]
    public decimal PartAOOP { get; set; }

    [JsonPropertyName("partBOOP")]
    public decimal PartBOOP { get; set; }

    [JsonPropertyName("partDOOP")]
    public decimal PartDOOP { get; set; }

    [JsonPropertyName("totalABMedicareAdvantage")]
    public decimal TotalABMedicareAdvantage { get; set; }

    [JsonPropertyName("reserveDaysLeft")]
    public int ReserveDaysLeft { get; set; }

    [JsonPropertyName("dentalPremium")]
    public decimal DentalPremium { get; set; }

    [JsonPropertyName("dentalOOP")]
    public decimal DentalOOP { get; set; }

    [JsonPropertyName("planGPremium")]
    public decimal PlanGPremium { get; set; }

    [JsonPropertyName("planFPremium")]
    public decimal PlanFPremium { get; set; }

    [JsonPropertyName("planNPremium")]
    public decimal PlanNPremium { get; set; }

    [JsonPropertyName("totalABGD")]
    public decimal TotalABGD { get; set; }

    [JsonPropertyName("totalABFD")]
    public decimal TotalABFD { get; set; }

    [JsonPropertyName("totalABND")]
    public decimal TotalABND { get; set; }

    [JsonPropertyName("totalABCD")]
    public decimal TotalABCD { get; set; }
}
