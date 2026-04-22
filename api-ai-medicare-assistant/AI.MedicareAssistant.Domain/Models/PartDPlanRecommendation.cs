namespace Domain.Models;

// ===== REQUEST =====

public class PartDPlanRecommendationRequest
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
    public decimal? StarRatingFilter { get; set; }
    public string? PrescriptionCoverageFilter { get; set; }
    public string? ContractIdFilter { get; set; }
    public bool MailOrderPharmacy { get; set; }
    public int RetirementYear { get; set; }
}

public class CountyCodeModel
{
    public string Zipcode { get; set; } = "";
    public string State { get; set; } = "";
    public string StateCode { get; set; } = "";
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string CountyCode { get; set; } = "";
    public string CountyName { get; set; } = "";
}

public class PrescriptionInput
{
    public string Rxcui { get; set; } = "";
    public string RefillDuration { get; set; } = "30";
    public int PrescriptionCount { get; set; } = 30;
    public string Ndc { get; set; } = "";
}

public class PharmacyInput
{
    public string PharmacyNumber { get; set; } = "";
    public string PharmacyName { get; set; } = "";
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
    public string Address { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Zipcode { get; set; } = "";
}

// ===== RESPONSE =====

public class PartDPlanRecommendationResponse
{
    public string WebServiceTransactionId { get; set; } = "";
    public string WebServiceStatus { get; set; } = "";
    public CountyCodeModel? CountycodeModel { get; set; }
    public List<PharmacyInput> Pharmacies { get; set; } = [];
    public List<PrescriptionInput> Prescriptions { get; set; } = [];
    public Dictionary<string, string> ContractIdCarrierMap { get; set; } = new();
    public Dictionary<string, List<string>> CarrierContractIdMap { get; set; } = new();
    public bool BeneficiaryCostDataRequired { get; set; }
    public string? ContractIdFilter { get; set; }
    public string? PlanNameFilter { get; set; }
    public string? PlanIdFilter { get; set; }
    public string? SegmentIdFilter { get; set; }
    public decimal? StarRatingFilter { get; set; }
    public object? PrescriptionCoverageFilter { get; set; }
    public int RecommendationPage { get; set; }
    public int RecommendationPageSize { get; set; }
    public int TotalRecommendationPages { get; set; }
    public int TotalRecommendations { get; set; }
    public string SortRecommendations { get; set; } = "";
    public int? RetirementYear { get; set; }
    public int? DataYear { get; set; }
    public double PartAPremium { get; set; }
    public double PartBPremium { get; set; }
    public double PartBOOP { get; set; }
    public double PartBPremiumSurcharge { get; set; }
    public int MonthsUsedForExpenseCalc { get; set; }
    public double PartDPremiumSurcharge { get; set; }
    public List<object>? Recommendations { get; set; }
    public List<RecommendationListItem> RecommendationList { get; set; } = [];
}

public class RecommendationListItem
{
    public string ContractId { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string SegmentId { get; set; } = "";
    public List<PharmacyWiseRecommendation> PharmacyWiseRecommendations { get; set; } = [];
}

public class PharmacyWiseRecommendation
{
    public string ContractId { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string PlanType { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string SegmentId { get; set; } = "";
    public string PharmacyNumber { get; set; } = "";
    public string PharmacyName { get; set; } = "";
    public string PharmacyRetailType { get; set; } = "";
    public double DispenseFee { get; set; }
    public double Premium { get; set; }
    public double Deductible { get; set; }
    public double Icl { get; set; }
    public double? StarRating { get; set; }
    public string WebsiteLink { get; set; } = "";
    public string ContactTitle { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Ext { get; set; } = "";
    public string Fax { get; set; } = "";
    public string Email { get; set; } = "";
    public List<object> DrugPriceCosts { get; set; } = [];
    public double TotalPremiumToPay { get; set; }
    public double TotalPrescriptionCost { get; set; }
    public double TotalPrescriptionCostFullYear { get; set; }
    public double TotalPlanCost { get; set; }
    public bool PrescriptionDrugCovered { get; set; }
    public double PartAandBBenefitServiceCost { get; set; }
    public double PartABenefitServiceCost { get; set; }
    public double PartBBenefitServiceCost { get; set; }
    public List<PlanExpenseMonth> PlanExpenses { get; set; } = [];
    public List<PlanExpenseOopMonth> PlanExpensesFullYear { get; set; } = [];
    public List<string> UnavailableDrugs { get; set; } = [];
    public List<PharmacyNetwork> PharmacyNetworks { get; set; } = [];
    public string LName { get; set; } = "";
    public string FName { get; set; } = "";
    public string MName { get; set; } = "";
}

public class PlanExpenseMonth
{
    public int Month { get; set; }
    public double Oop { get; set; }
    public double Premium { get; set; }
    public double DrugRetailCost { get; set; }
}

public class PlanExpenseOopMonth
{
    public int Month { get; set; }
    public double Oop { get; set; }
}

public class PharmacyNetwork
{
    public string PharmacyNumber { get; set; } = "";
    public string PharmacyName { get; set; } = "";
    public string PharmacyNetworkType { get; set; } = "";
    public string Distance { get; set; } = "";
}
 