using Domain.Models;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for PlanRecommendation domain models — serialization, construction, and data integrity.
/// Uses realistic sample data matching what the AI and CMS services return.
/// </summary>
public class PlanRecommendationModelTests
{
    // ═══════ PlanRecommendationRequest ═══════

    [Fact]
    public void PlanRecommendationRequest_ConstructsCorrectly()
    {
        var request = CreateSampleRequest();

        Assert.Equal("user-123", request.UserId);
        Assert.Equal("10001", request.ZipCode);
        Assert.Equal("New York", request.County);
        Assert.Equal(3, request.RxCuis.Count);
        Assert.Equal("Low", request.MagiTier);
        Assert.Equal(20000m, request.AnnualIncome);
        Assert.Equal(1, request.HouseholdSize);
        Assert.Equal("Single", request.IncomeFilingStatus);
        Assert.False(request.HasEmployerCoverage);
        Assert.Equal(67, request.Age);
        Assert.Equal(3, request.DrugSummaries.Count);
    }

    [Fact]
    public void DrugSummary_StoresAllFields()
    {
        var summary = new DrugSummary(
            RxCui: "197361",
            DrugName: "Lisinopril",
            GenericName: "lisinopril",
            SelectedName: "Lisinopril",
            NameType: "generic",
            TherapeuticCategory: "Cardiovascular",
            DrugClass: "ACE Inhibitor",
            DosageForm: "Tablet",
            Strength: "10 mg",
            Packaging: "Bottle of 90",
            Ndc: "00093-7182-01"
        );

        Assert.Equal("197361", summary.RxCui);
        Assert.Equal("Lisinopril", summary.DrugName);
        Assert.Equal("lisinopril", summary.GenericName);
        Assert.Equal("00093-7182-01", summary.Ndc);
        Assert.Equal("ACE Inhibitor", summary.DrugClass);
        Assert.Equal("Tablet", summary.DosageForm);
    }

    // ═══════ PlanRecommendationResult ═══════

    [Fact]
    public void PlanRecommendationResult_DefaultValues()
    {
        var result = new PlanRecommendationResult();

        Assert.False(result.LisEligible);
        Assert.Equal("None", result.LisTier);
        Assert.Equal("", result.RecommendedPlanType);
        Assert.Equal("", result.EligibilitySummary);
        Assert.Null(result.LisCallToAction);
        Assert.Empty(result.RankedPlans);
    }

    [Fact]
    public void PlanRecommendationResult_WithSampleData()
    {
        var result = CreateSampleResult();

        Assert.True(result.LisEligible);
        Assert.Equal("Full", result.LisTier);
        Assert.Equal("MedicareAdvantage", result.RecommendedPlanType);
        Assert.NotEmpty(result.EligibilitySummary);
        Assert.NotNull(result.LisCallToAction);
        Assert.Equal(3, result.RankedPlans.Count);
    }

    // ═══════ RankedPlan ═══════

    [Fact]
    public void RankedPlan_BestValuePlan_HasLowestTotalCost()
    {
        var result = CreateSampleResult();
        var sorted = result.RankedPlans.OrderBy(p => p.EstimatedAnnualTotalCost).ToList();

        Assert.Equal("Humana Gold Plus", sorted[0].PlanName);
        Assert.Equal(1380m, sorted[0].EstimatedAnnualTotalCost);
    }

    [Fact]
    public void RankedPlan_AllPlansHaveDrugCoverages()
    {
        var result = CreateSampleResult();

        foreach (var plan in result.RankedPlans)
        {
            Assert.NotEmpty(plan.DrugCoverages);
            Assert.True(plan.DrugCoverages.Count >= 2, $"Plan {plan.PlanName} should cover at least 2 drugs");
        }
    }

    [Fact]
    public void RankedPlan_StarRatings_AreValid()
    {
        var result = CreateSampleResult();

        foreach (var plan in result.RankedPlans)
        {
            Assert.True(double.TryParse(plan.StarRating, out var rating), $"StarRating '{plan.StarRating}' should be parseable");
            Assert.InRange(rating, 1.0, 5.0);
        }
    }

    [Fact]
    public void RankedPlan_PlanTypes_AreValid()
    {
        var result = CreateSampleResult();
        var validTypes = new[] { "MA-PD", "PDP", "D-SNP", "MedicareAdvantage", "PartDPlusMedigap", "DSNPDualEligible" };

        foreach (var plan in result.RankedPlans)
        {
            Assert.Contains(plan.PlanType, validTypes);
        }
    }

    // ═══════ PlanDrugCoverage ═══════

    [Fact]
    public void PlanDrugCoverage_CoveredDrug_HasValidTierAndCopay()
    {
        var coverage = new PlanDrugCoverage
        {
            DrugName = "Lisinopril",
            RxCui = "197361",
            IsCovered = true,
            FormularyTier = 1,
            MonthlyCopay = 0m,
            RequiresPriorAuth = false,
            HasQuantityLimit = false
        };

        Assert.True(coverage.IsCovered);
        Assert.Equal(1, coverage.FormularyTier);
        Assert.Equal(0m, coverage.MonthlyCopay);
        Assert.False(coverage.RequiresPriorAuth);
    }

    [Fact]
    public void PlanDrugCoverage_UncoveredDrug()
    {
        var coverage = new PlanDrugCoverage
        {
            DrugName = "ExperimentalDrug",
            RxCui = "999999",
            IsCovered = false,
            FormularyTier = 0,
            MonthlyCopay = 0m
        };

        Assert.False(coverage.IsCovered);
    }

    [Fact]
    public void PlanDrugCoverage_SpecialtyDrug_Tier5()
    {
        var coverage = new PlanDrugCoverage
        {
            DrugName = "Humira",
            RxCui = "327361",
            IsCovered = true,
            FormularyTier = 5,
            MonthlyCopay = 250m,
            RequiresPriorAuth = true,
            HasQuantityLimit = true,
            QuantityLimitDetail = "1 pen per 14 days"
        };

        Assert.True(coverage.IsCovered);
        Assert.Equal(5, coverage.FormularyTier);
        Assert.Equal(250m, coverage.MonthlyCopay);
        Assert.True(coverage.RequiresPriorAuth);
        Assert.True(coverage.HasQuantityLimit);
        Assert.Equal("1 pen per 14 days", coverage.QuantityLimitDetail);
    }

    // ═══════ CMS Plan Info (Phase 2) ═══════

    [Fact]
    public void CmsPlanInfo_StoresAllFields()
    {
        var plan = new CmsPlanInfo
        {
            ContractId = "H1234",
            PlanId = "001",
            PlanName = "Aetna Medicare Advantage",
            OrganizationName = "Aetna Inc",
            PlanType = "MA-PD",
            MonthlyPremium = 35.00m,
            AnnualDeductible = 250.00m,
            StarRating = 4.5m,
            StateCode = "NY",
            CountyName = "New York"
        };

        Assert.Equal("H1234", plan.ContractId);
        Assert.Equal(35.00m, plan.MonthlyPremium);
        Assert.Equal(4.5m, plan.StarRating);
    }

    // ═══════ CMS Formulary Entry (Phase 2) ═══════

    [Fact]
    public void CmsFormularyEntry_StoresAllFields()
    {
        var entry = new CmsFormularyEntry
        {
            RxCui = "197361",
            DrugName = "Lisinopril",
            ContractId = "H1234",
            FormularyTier = 1,
            RequiresPriorAuth = false,
            HasQuantityLimit = false,
            HasStepTherapy = false
        };

        Assert.Equal("197361", entry.RxCui);
        Assert.Equal(1, entry.FormularyTier);
        Assert.False(entry.RequiresPriorAuth);
        Assert.False(entry.HasStepTherapy);
    }

    // ═══════ LisTier enum ═══════

    [Fact]
    public void LisTier_HasExpectedValues()
    {
        Assert.Equal(0, (int)LisTier.None);
        Assert.Equal(1, (int)LisTier.Partial);
        Assert.Equal(2, (int)LisTier.Full);
    }

    // ═══════ Helpers ═══════

    public static PlanRecommendationRequest CreateSampleRequest()
    {
        return new PlanRecommendationRequest(
            UserId: "user-123",
            ZipCode: "10001",
            County: "New York",
            RxCuis: ["197361", "312961", "198211"],
            MagiTier: "Low",
            AnnualIncome: 20000m,
            HouseholdSize: 1,
            IncomeFilingStatus: "Single",
            HasEmployerCoverage: false,
            DisabilityStatus: "None",
            HasChronicCondition: true,
            ChronicConditionDetails: "Hypertension, Type 2 Diabetes",
            RetirementAge: 65,
            Age: 67,
            DrugSummaries:
            [
                new DrugSummary("197361", "Lisinopril", "lisinopril", null, null, "Cardiovascular", "ACE Inhibitor", null, null, null, "00093-7182-01"),
                new DrugSummary("312961", "Metformin", "metformin", null, null, "Antidiabetic", "Biguanide", null, null, null, "00093-1048-01"),
                new DrugSummary("198211", "Atorvastatin", "atorvastatin", null, null, "Cardiovascular", "Statin", null, null, null, "00093-5057-01")
            ]
        );
    }

    public static PlanRecommendationResult CreateSampleResult()
    {
        return new PlanRecommendationResult
        {
            LisEligible = true,
            LisTier = "Full",
            RecommendedPlanType = "MedicareAdvantage",
            EligibilitySummary = "Based on your income of $20,000 and household size of 1, you qualify for Full Extra Help (LIS). Your drug copays will be significantly reduced.",
            LisCallToAction = "Contact Social Security at 1-800-772-1213 to apply for Extra Help.",
            RankedPlans =
            [
                new RankedPlan
                {
                    PlanId = "H1234-001",
                    PlanName = "Humana Gold Plus",
                    PlanType = "MA-PD",
                    InsuranceName = "Humana",
                    MonthlyPremium = 0m,
                    AnnualDeductible = 0m,
                    AnnualMoop = 3900m,
                    EstimatedAnnualDrugCost = 1380m,
                    EstimatedAnnualTotalCost = 1380m,
                    StarRating = "4.5",
                    HasPreferredPharmacyNetwork = true,
                    PlanFinderUrl = "https://www.medicare.gov/plan-compare",
                    AiExplanation = "Best value for your drug list with $0 premium and low copays.",
                    DrugCoverages =
                    [
                        new PlanDrugCoverage { DrugName = "Lisinopril", RxCui = "197361", IsCovered = true, FormularyTier = 1, MonthlyCopay = 0m },
                        new PlanDrugCoverage { DrugName = "Metformin", RxCui = "312961", IsCovered = true, FormularyTier = 1, MonthlyCopay = 0m },
                        new PlanDrugCoverage { DrugName = "Atorvastatin", RxCui = "198211", IsCovered = true, FormularyTier = 2, MonthlyCopay = 5m }
                    ]
                },
                new RankedPlan
                {
                    PlanId = "S5678-002",
                    PlanName = "Aetna CVS Health Medicare",
                    PlanType = "MA-PD",
                    InsuranceName = "Aetna",
                    MonthlyPremium = 25m,
                    AnnualDeductible = 200m,
                    AnnualMoop = 5000m,
                    EstimatedAnnualDrugCost = 1560m,
                    EstimatedAnnualTotalCost = 1860m,
                    StarRating = "4.0",
                    HasPreferredPharmacyNetwork = true,
                    PlanFinderUrl = "https://www.medicare.gov/plan-compare",
                    AiExplanation = "Good coverage with CVS preferred pharmacy network.",
                    DrugCoverages =
                    [
                        new PlanDrugCoverage { DrugName = "Lisinopril", RxCui = "197361", IsCovered = true, FormularyTier = 1, MonthlyCopay = 0m },
                        new PlanDrugCoverage { DrugName = "Metformin", RxCui = "312961", IsCovered = true, FormularyTier = 1, MonthlyCopay = 3m },
                        new PlanDrugCoverage { DrugName = "Atorvastatin", RxCui = "198211", IsCovered = true, FormularyTier = 2, MonthlyCopay = 10m }
                    ]
                },
                new RankedPlan
                {
                    PlanId = "R9012-003",
                    PlanName = "UnitedHealthcare AARP PDP",
                    PlanType = "PDP",
                    InsuranceName = "UnitedHealthcare",
                    MonthlyPremium = 15m,
                    AnnualDeductible = 505m,
                    AnnualMoop = 0m,
                    EstimatedAnnualDrugCost = 1800m,
                    EstimatedAnnualTotalCost = 1980m,
                    StarRating = "3.5",
                    HasPreferredPharmacyNetwork = false,
                    PlanFinderUrl = "https://www.medicare.gov/plan-compare",
                    AiExplanation = "Standalone Part D with broad formulary coverage.",
                    DrugCoverages =
                    [
                        new PlanDrugCoverage { DrugName = "Lisinopril", RxCui = "197361", IsCovered = true, FormularyTier = 2, MonthlyCopay = 5m },
                        new PlanDrugCoverage { DrugName = "Metformin", RxCui = "312961", IsCovered = true, FormularyTier = 1, MonthlyCopay = 0m },
                        new PlanDrugCoverage { DrugName = "Atorvastatin", RxCui = "198211", IsCovered = true, FormularyTier = 3, MonthlyCopay = 35m }
                    ]
                }
            ]
        };
    }
}
