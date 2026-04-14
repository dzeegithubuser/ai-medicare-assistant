using Application.Services;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for MedicarePlanService — enrichment with CMS data and LIS tier determination.
/// Uses Moq to isolate from external AI and CMS dependencies.
/// </summary>
public class MedicarePlanServiceTests
{
    private readonly Mock<ICountyLookupService> _countyMock;
    private readonly Mock<IPlanScoringAiService> _aiScoringMock;
    private readonly Mock<ICmsPlanDataService> _cmsPlanDataMock;
    private readonly Mock<ILogger<MedicarePlanService>> _loggerMock;

    public MedicarePlanServiceTests()
    {
        _countyMock = new Mock<ICountyLookupService>();
        _aiScoringMock = new Mock<IPlanScoringAiService>();
        _cmsPlanDataMock = new Mock<ICmsPlanDataService>();
        _loggerMock = new Mock<ILogger<MedicarePlanService>>();
    }

    // ═══════ DetermineLisTier (static, directly testable) ═══════

    [Theory]
    [InlineData(15000, 1, "Single", "Full")]     // Well below full limit
    [InlineData(22590, 1, "Single", "Full")]     // Exactly at full limit
    [InlineData(22591, 1, "Single", "Partial")]  // Just above full limit
    [InlineData(33240, 1, "Single", "Partial")]  // Exactly at partial limit
    [InlineData(33241, 1, "Single", "None")]     // Just above partial limit
    [InlineData(50000, 1, "Single", "None")]     // Well above both limits
    public void DetermineLisTier_SinglePerson(decimal income, int household, string filing, string expected)
    {
        var result = InvokeDetermineLisTier(income, household, filing);
        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData(30660, 2, "MarriedFilingJointly", "Full")]   // 22590 + 8070 = 30660
    [InlineData(30661, 2, "MarriedFilingJointly", "Partial")]
    [InlineData(44880, 2, "MarriedFilingJointly", "Partial")] // 33240 + 11640 = 44880
    [InlineData(44881, 2, "MarriedFilingJointly", "None")]
    public void DetermineLisTier_HouseholdOfTwo(decimal income, int household, string filing, string expected)
    {
        var result = InvokeDetermineLisTier(income, household, filing);
        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData(46800, 4, "Head", "Full")]   // 22590 + 3*8070 = 46800
    [InlineData(68160, 4, "Head", "Partial")] // 33240 + 3*11640 = 68160
    [InlineData(68161, 4, "Head", "None")]
    public void DetermineLisTier_HouseholdOfFour(decimal income, int household, string filing, string expected)
    {
        var result = InvokeDetermineLisTier(income, household, filing);
        Assert.Equal(expected, result.ToString());
    }

    [Fact]
    public void DetermineLisTier_ZeroIncome_ReturnsFull()
    {
        var result = InvokeDetermineLisTier(0m, 1, "Single");
        Assert.Equal(LisTier.Full, result);
    }

    [Fact]
    public void DetermineLisTier_NegativeHousehold_TreatedAsOne()
    {
        // Uses Math.Max(1, householdSize), so -1 → 1
        var result = InvokeDetermineLisTier(22590m, -1, "Single");
        Assert.Equal(LisTier.Full, result);
    }

    // ═══════ RecommendPlansAsync (integration with mocked dependencies) ═══════

    [Fact]
    public async Task RecommendPlansAsync_SetsLisFieldsFromDeterministicComputation()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest();

        SetupFips("36061", "New York", "NY");
        SetupAiScoring(CreateAiResult());
        SetupCmsEmpty();

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert — income $20,000, household 1 → Full LIS
        Assert.True(result.LisEligible);
        Assert.Equal("Full", result.LisTier);
        Assert.Contains("Full Extra Help", result.LisCallToAction);
        Assert.Contains("ssa.gov", result.LisCallToAction);
    }

    [Fact]
    public async Task RecommendPlansAsync_HighIncome_NoLisEligibility()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest() with
        {
            AnnualIncome = 80000m
        };

        SetupFips("36061", "New York", "NY");
        SetupAiScoring(CreateAiResult());
        SetupCmsEmpty();

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert
        Assert.False(result.LisEligible);
        Assert.Equal("None", result.LisTier);
    }

    [Fact]
    public async Task RecommendPlansAsync_PartialLis_SetsCorrectCallToAction()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest() with
        {
            AnnualIncome = 25000m  // Above $22,590 (full) but below $33,240 (partial)
        };

        SetupFips("36061", "New York", "NY");
        SetupAiScoring(CreateAiResult());
        SetupCmsEmpty();

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert
        Assert.True(result.LisEligible);
        Assert.Equal("Partial", result.LisTier);
        Assert.Contains("Partial Extra Help", result.LisCallToAction);
    }

    [Fact]
    public async Task RecommendPlansAsync_NoFipsMapping_StillProducesResult()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest();

        _countyMock.Setup(f => f.GetCountyCode(It.IsAny<string>())).ReturnsAsync((string?)null);
        _countyMock.Setup(f => f.GetCountyName(It.IsAny<string>())).ReturnsAsync((string?)null);
        _countyMock.Setup(f => f.GetStateCode(It.IsAny<string>())).ReturnsAsync((string?)null);
        SetupAiScoring(CreateAiResult());
        SetupCmsEmpty();

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert — result should still be produced with AI data
        Assert.NotNull(result);
        Assert.NotEmpty(result.RankedPlans);
    }

    // ═══════ CMS Enrichment ═══════

    [Fact]
    public async Task RecommendPlansAsync_EnrichesWithCmsData_OverridesPremium()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest();

        SetupFips("36061", "New York", "NY");

        var aiResult = CreateAiResult();
        aiResult.RankedPlans[0].PlanName = "Humana Gold Plus";
        aiResult.RankedPlans[0].InsuranceName = "Humana";
        aiResult.RankedPlans[0].MonthlyPremium = 99m; // AI estimate

        SetupAiScoring(aiResult);

        // CMS data with different premium
        _cmsPlanDataMock.Setup(c => c.GetPlansForAreaAsync("NY", "New York", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CmsPlanInfo>
            {
                new()
                {
                    ContractId = "H1234",
                    PlanId = "001",
                    PlanName = "Humana Gold Plus HMO",
                    OrganizationName = "Humana",
                    MonthlyPremium = 0m,      // Real CMS data: $0 premium
                    AnnualDeductible = 0m,
                    StarRating = 4.5m
                }
            });

        _cmsPlanDataMock.Setup(c => c.GetFormularyEntriesAsync("H1234", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CmsFormularyEntry>());

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert — Premium should be overridden by CMS data
        var humana = result.RankedPlans.FirstOrDefault(p => p.PlanName == "Humana Gold Plus");
        Assert.NotNull(humana);
        Assert.Equal(0m, humana.MonthlyPremium); // CMS override, not AI's $99
    }

    [Fact]
    public async Task RecommendPlansAsync_EnrichesFormularyTiers()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest();

        SetupFips("36061", "New York", "NY");

        var aiResult = CreateAiResult();
        aiResult.RankedPlans[0].PlanName = "Humana Gold Plus";
        aiResult.RankedPlans[0].InsuranceName = "Humana";
        // AI says Lisinopril is tier 2
        aiResult.RankedPlans[0].DrugCoverages[0].FormularyTier = 2;

        SetupAiScoring(aiResult);

        _cmsPlanDataMock.Setup(c => c.GetPlansForAreaAsync("NY", "New York", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CmsPlanInfo>
            {
                new()
                {
                    ContractId = "H1234",
                    PlanId = "001",
                    PlanName = "Humana Gold Plus HMO",
                    OrganizationName = "Humana",
                    MonthlyPremium = 0m,
                    AnnualDeductible = 0m,
                    StarRating = 4.5m
                }
            });

        // CMS says Lisinopril is actually tier 1
        _cmsPlanDataMock.Setup(c => c.GetFormularyEntriesAsync("H1234", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CmsFormularyEntry>
            {
                new()
                {
                    RxCui = "197361",
                    DrugName = "Lisinopril",
                    ContractId = "H1234",
                    FormularyTier = 1,
                    RequiresPriorAuth = false,
                    HasQuantityLimit = false,
                    HasStepTherapy = false
                }
            });

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert — Formulary tier should be updated to CMS value
        var humana = result.RankedPlans.FirstOrDefault(p => p.PlanName == "Humana Gold Plus");
        Assert.NotNull(humana);
        var lisinopril = humana.DrugCoverages.FirstOrDefault(d => d.RxCui == "197361");
        Assert.NotNull(lisinopril);
        Assert.Equal(1, lisinopril.FormularyTier); // CMS override: tier 1
        Assert.True(lisinopril.IsCovered);
    }

    [Fact]
    public async Task RecommendPlansAsync_NoStateCode_SkipsCmsEnrichment()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest();

        _countyMock.Setup(f => f.GetCountyCode("10001")).ReturnsAsync("36061");
        _countyMock.Setup(f => f.GetCountyName("10001")).ReturnsAsync("New York");
        _countyMock.Setup(f => f.GetStateCode("10001")).ReturnsAsync((string?)null); // No state

        var aiResult = CreateAiResult();
        aiResult.RankedPlans[0].MonthlyPremium = 99m;
        SetupAiScoring(aiResult);

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert — AI premium should remain unchanged since CMS was skipped
        // (LIS adjustments may re-sort, so find the plan by name)
        var plan = result.RankedPlans.First(p => p.PlanName == "Humana Gold Plus");
        Assert.Equal(99m, plan.MonthlyPremium);

        // CMS service should never have been called
        _cmsPlanDataMock.Verify(c => c.GetPlansForAreaAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecommendPlansAsync_ReSortsByTotalCostAfterEnrichment()
    {
        // Arrange
        var sut = CreateServiceWithMockedDeps();
        var request = PlanRecommendationModelTests.CreateSampleRequest();

        SetupFips("36061", "New York", "NY");

        var aiResult = CreateAiResult();
        // AI has Plan A first (lower AI cost), Plan B second (higher AI cost)
        aiResult.RankedPlans =
        [
            new RankedPlan
            {
                PlanName = "Plan A",
                InsuranceName = "Insurer A",
                PlanType = "MA-PD",
                MonthlyPremium = 50m,
                EstimatedAnnualDrugCost = 500m,
                EstimatedAnnualTotalCost = 1100m,
                StarRating = "4.0",
                DrugCoverages =
                [
                    new PlanDrugCoverage { DrugName = "Drug1", RxCui = "111", IsCovered = true, FormularyTier = 1, MonthlyCopay = 5m },
                    new PlanDrugCoverage { DrugName = "Drug2", RxCui = "222", IsCovered = true, FormularyTier = 3, MonthlyCopay = 45m }
                ]
            },
            new RankedPlan
            {
                PlanName = "Plan B",
                InsuranceName = "Insurer B",
                PlanType = "MA-PD",
                MonthlyPremium = 100m,
                EstimatedAnnualDrugCost = 500m,
                EstimatedAnnualTotalCost = 1700m,
                StarRating = "4.5",
                DrugCoverages =
                [
                    new PlanDrugCoverage { DrugName = "Drug1", RxCui = "111", IsCovered = true, FormularyTier = 1, MonthlyCopay = 5m },
                    new PlanDrugCoverage { DrugName = "Drug2", RxCui = "222", IsCovered = true, FormularyTier = 3, MonthlyCopay = 45m }
                ]
            }
        ];

        SetupAiScoring(aiResult);

        // CMS says Plan B is actually $0 premium → total = $500, Plan A isn't in CMS
        _cmsPlanDataMock.Setup(c => c.GetPlansForAreaAsync("NY", "New York", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CmsPlanInfo>
            {
                new()
                {
                    ContractId = "H9999",
                    PlanId = "002",
                    PlanName = "Plan B",
                    OrganizationName = "Insurer B",
                    MonthlyPremium = 0m,
                    AnnualDeductible = 0m,
                    StarRating = 4.5m
                }
            });

        _cmsPlanDataMock.Setup(c => c.GetFormularyEntriesAsync("H9999", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CmsFormularyEntry>());

        // Act
        var result = await sut.RecommendPlansAsync(request);

        // Assert — Plan B should be sorted first now (CMS: $0 premium)
        // LIS adjustments (Full LIS, income $20K > $15,060 FPL): $4.50 generic, $11.20 brand
        // Drug costs per plan: Drug1(T1,$4.50) + Drug2(T3,$11.20) = $15.70/mo = $188.40/yr
        // Plan B total: $0*12 + $0 + $188.40 = $188.40
        // Plan A total: $50*12 + $0 + $188.40 = $788.40
        Assert.Equal("Plan B", result.RankedPlans[0].PlanName);
        Assert.Equal(188.40m, result.RankedPlans[0].EstimatedAnnualTotalCost);
        Assert.Equal("Plan A", result.RankedPlans[1].PlanName);
    }

    // ═══════ Helpers ═══════

    private static LisTier InvokeDetermineLisTier(decimal income, int household, string filing)
    {
        return MedicarePlanService.DetermineLisTier(income, household, filing);
    }

    private void SetupFips(string fips, string countyName, string stateCode)
    {
        _countyMock.Setup(f => f.GetCountyCode(It.IsAny<string>())).ReturnsAsync(fips);
        _countyMock.Setup(f => f.GetCountyName(It.IsAny<string>())).ReturnsAsync(countyName);
        _countyMock.Setup(f => f.GetStateCode(It.IsAny<string>())).ReturnsAsync(stateCode);
    }

    private void SetupAiScoring(PlanRecommendationResult result)
    {
        _aiScoringMock.Setup(a => a.ScorePlansAsync(
                It.IsAny<PlanRecommendationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupCmsEmpty()
    {
        _cmsPlanDataMock.Setup(c => c.GetPlansForAreaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CmsPlanInfo>());
    }

    private MedicarePlanService CreateServiceWithMockedDeps()
    {
        // ProfileService is not called in RecommendPlansAsync path
        // but needed for construction. We pass null since it won't be called in our tests.

        return new MedicarePlanService(
            _countyMock.Object,
            _aiScoringMock.Object,
            _cmsPlanDataMock.Object,
            null!, // profileService — not used in RecommendPlansAsync
            _loggerMock.Object);
    }

    private static PlanRecommendationResult CreateAiResult()
    {
        return new PlanRecommendationResult
        {
            LisEligible = false,
            LisTier = "None",
            RecommendedPlanType = "MedicareAdvantage",
            EligibilitySummary = "AI-generated summary",
            RankedPlans =
            [
                new RankedPlan
                {
                    PlanId = "AI-001",
                    PlanName = "Humana Gold Plus",
                    InsuranceName = "Humana",
                    PlanType = "MA-PD",
                    MonthlyPremium = 0m,
                    AnnualDeductible = 0m,
                    EstimatedAnnualDrugCost = 1380m,
                    EstimatedAnnualTotalCost = 1380m,
                    StarRating = "4.5",
                    AiExplanation = "Best value plan",
                    DrugCoverages =
                    [
                        new PlanDrugCoverage { DrugName = "Lisinopril", RxCui = "197361", IsCovered = true, FormularyTier = 1, MonthlyCopay = 0m },
                        new PlanDrugCoverage { DrugName = "Metformin", RxCui = "312961", IsCovered = true, FormularyTier = 1, MonthlyCopay = 0m },
                        new PlanDrugCoverage { DrugName = "Atorvastatin", RxCui = "198211", IsCovered = true, FormularyTier = 2, MonthlyCopay = 5m }
                    ]
                },
                new RankedPlan
                {
                    PlanId = "AI-002",
                    PlanName = "Aetna CVS Health Medicare",
                    InsuranceName = "Aetna",
                    PlanType = "MA-PD",
                    MonthlyPremium = 25m,
                    AnnualDeductible = 200m,
                    EstimatedAnnualDrugCost = 1560m,
                    EstimatedAnnualTotalCost = 1860m,
                    StarRating = "4.0",
                    AiExplanation = "Good CVS coverage",
                    DrugCoverages =
                    [
                        new PlanDrugCoverage { DrugName = "Lisinopril", RxCui = "197361", IsCovered = true, FormularyTier = 1, MonthlyCopay = 0m },
                        new PlanDrugCoverage { DrugName = "Metformin", RxCui = "312961", IsCovered = true, FormularyTier = 1, MonthlyCopay = 3m }
                    ]
                }
            ]
        };
    }
}
