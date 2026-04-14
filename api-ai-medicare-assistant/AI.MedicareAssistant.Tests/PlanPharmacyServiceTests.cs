using Application.Services;
using Domain.Interfaces;
using Domain.Models.Pharmacy;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

public class PlanPharmacyServiceTests
{
    private readonly Mock<IPharmacyPricingService> _pharmacyServiceMock;
    private readonly Mock<ILogger<PlanPharmacyService>> _loggerMock;
    private readonly PlanPharmacyService _sut;

    public PlanPharmacyServiceTests()
    {
        _pharmacyServiceMock = new Mock<IPharmacyPricingService>();
        _loggerMock = new Mock<ILogger<PlanPharmacyService>>();
        _sut = new PlanPharmacyService(_pharmacyServiceMock.Object, _loggerMock.Object);
    }

    // ═══════ Basic enrichment ═══════

    [Fact]
    public async Task GetPlanPharmaciesAsync_EnrichesWithCopaysFromCoverage()
    {
        // Arrange
        var drugs = CreateDrugInputs();
        var coverages = CreateCoverages();
        SetupPharmacies(CreateCvsPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert
        Assert.Single(result);
        var pharmacy = result[0];
        var lisinopril = pharmacy.Drugs.First(d => d.DrugName == "Lisinopril");
        Assert.Equal(0m, lisinopril.PlanCopay); // CVS preferred → 0 * 0.8 = 0
        Assert.Equal(1, lisinopril.FormularyTier);
        Assert.False(lisinopril.RequiresPriorAuth);
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_PreferredPharmacy_Gets20PercentCopayReduction()
    {
        // Arrange
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("197361", "Lisinopril", 2, 10m, true, false), // $10 copay
            new("312961", "Metformin", 1, 5m, true, false),   // $5 copay
        };
        SetupPharmacies(CreateCvsPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert
        var pharmacy = result[0];
        Assert.True(pharmacy.IsPreferredNetwork);

        var lisinopril = pharmacy.Drugs.First(d => d.DrugName == "Lisinopril");
        Assert.Equal(8m, lisinopril.PlanCopay); // 10 * 0.8 = 8

        var metformin = pharmacy.Drugs.First(d => d.DrugName == "Metformin");
        Assert.Equal(4m, metformin.PlanCopay); // 5 * 0.8 = 4
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_NonPreferredPharmacy_GetsFullCopay()
    {
        // Arrange
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("197361", "Lisinopril", 2, 10m, true, false),
        };
        SetupPharmacies(CreateIndependentPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert
        var pharmacy = result[0];
        Assert.False(pharmacy.IsPreferredNetwork);

        var lisinopril = pharmacy.Drugs.First(d => d.DrugName == "Lisinopril");
        Assert.Equal(10m, lisinopril.PlanCopay); // Full copay, no 20% reduction
    }

    // ═══════ Preferred pharmacy detection (chain matching) ═══════

    [Theory]
    [InlineData("CVS Pharmacy #1234", "COMMUNITY/RETAIL PHARMACY", true)]
    [InlineData("WALGREENS #5678", "COMMUNITY/RETAIL PHARMACY", true)]
    [InlineData("Walmart Pharmacy", "CHAIN PHARMACY", true)]
    [InlineData("RITE AID", "COMMUNITY/RETAIL PHARMACY", true)]
    [InlineData("COSTCO Pharmacy", "CHAIN PHARMACY", true)]
    [InlineData("KROGER PHARMACY", "CHAIN PHARMACY", true)]
    [InlineData("Target Pharmacy", "CHAIN PHARMACY", true)]
    [InlineData("Sam's Club Pharmacy", "CHAIN PHARMACY", true)]
    [InlineData("Mom and Pop Drugs", "INDEPENDENT", false)]
    [InlineData("Local Health Pharmacy", "INDEPENDENT", false)]
    public async Task GetPlanPharmaciesAsync_DetectsPreferredPharmacy(string pharmacyName, string pharmacyType, bool expectedPreferred)
    {
        // Arrange
        var drugs = CreateDrugInputs();
        var coverages = CreateCoverages();
        var pharmacy = CreatePharmacyWithPricing(pharmacyName, pharmacyType, drugs);
        SetupPharmacies(pharmacy);

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert
        Assert.Equal(expectedPreferred, result[0].IsPreferredNetwork);
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_CommunityRetailType_IsPreferred()
    {
        // Arrange: Even if name doesn't match a chain, "COMMUNITY/RETAIL" type makes it preferred
        var drugs = CreateDrugInputs();
        var coverages = CreateCoverages();
        var pharmacy = CreatePharmacyWithPricing("Some Unknown Pharmacy", "Community/Retail Pharmacy", drugs);
        SetupPharmacies(pharmacy);

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert
        Assert.True(result[0].IsPreferredNetwork);
    }

    // ═══════ Sorting ═══════

    [Fact]
    public async Task GetPlanPharmaciesAsync_SortsPreferredFirst_ThenByCopay()
    {
        // Arrange: 3 pharmacies — non-preferred (low copay), preferred (high copay), preferred (low copay)
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("197361", "Lisinopril", 2, 20m, true, false),
            new("312961", "Metformin", 1, 0m, true, false),
        };

        var pharmacies = new List<PharmacyWithPricing>
        {
            CreatePharmacyWithPricing("Local Independent Rx", "INDEPENDENT", drugs),
            CreatePharmacyWithPricing("CVS Pharmacy #999", "COMMUNITY/RETAIL PHARMACY", drugs),
            CreatePharmacyWithPricing("WALGREENS #888", "COMMUNITY/RETAIL PHARMACY", drugs),
        };

        _pharmacyServiceMock.Setup(s => s.GetPharmaciesWithPricingAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<DrugPricingInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pharmacies);

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert — preferred pharmacies come first
        Assert.Equal(3, result.Count);
        Assert.True(result[0].IsPreferredNetwork);
        Assert.True(result[1].IsPreferredNetwork);
        Assert.False(result[2].IsPreferredNetwork);
    }

    // ═══════ Edge cases ═══════

    [Fact]
    public async Task GetPlanPharmaciesAsync_NoPharmaciesFound_ReturnsEmpty()
    {
        _pharmacyServiceMock.Setup(s => s.GetPharmaciesWithPricingAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<DrugPricingInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PharmacyWithPricing>());

        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001",
            CreateDrugInputs(), CreateCoverages());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_NoCoverageMatch_DrugUnchanged()
    {
        // Arrange: coverage RxCui doesn't match any drug
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("999999", "NonExistentDrug", 3, 50m, true, false),
        };
        SetupPharmacies(CreateCvsPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert — drugs should have no plan data set
        var pharmacy = result[0];
        Assert.All(pharmacy.Drugs, d =>
        {
            Assert.Null(d.PlanCopay);
            Assert.Null(d.FormularyTier);
        });
        Assert.Null(pharmacy.TotalPlanCopay); // no copay data → null
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_UncoveredDrug_NoCopay()
    {
        // Arrange: drug matches by RxCui but IsCovered = false
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("197361", "Lisinopril", 0, 0m, false, false), // NOT covered
        };
        SetupPharmacies(CreateCvsPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert — PlanCopay should be null when IsCovered = false
        var lisinopril = result[0].Drugs.First(d => d.DrugName == "Lisinopril");
        Assert.Null(lisinopril.PlanCopay); // IsCovered=false → copay not set
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_TotalPlanCopay_SumsAllCoveredDrugs()
    {
        // Arrange
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("197361", "Lisinopril", 2, 10m, true, false),
            new("312961", "Metformin", 1, 5m, true, false),
        };
        SetupPharmacies(CreateIndependentPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert — non-preferred, so full copays: 10 + 5 = 15
        Assert.Equal(15m, result[0].TotalPlanCopay);
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_MatchesByDrugName_CaseInsensitive()
    {
        // Arrange: coverage has different-case drug name
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("000000", "lisinopril", 2, 10m, true, false), // different RxCui, matches by name
        };
        SetupPharmacies(CreateIndependentPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert
        var lisinopril = result[0].Drugs.First(d => d.DrugName == "Lisinopril");
        Assert.Equal(10m, lisinopril.PlanCopay); // matched by name, non-preferred = full copay
    }

    [Fact]
    public async Task GetPlanPharmaciesAsync_ZeroCopay_NoReduction()
    {
        // Arrange: $0 copay should remain $0 even at preferred pharmacy
        var drugs = CreateDrugInputs();
        var coverages = new List<PlanDrugCoverageInput>
        {
            new("197361", "Lisinopril", 1, 0m, true, false),
        };
        SetupPharmacies(CreateCvsPharmacy(drugs));

        // Act
        var result = await _sut.GetPlanPharmaciesAsync("H1234-001", "10001", drugs, coverages);

        // Assert
        var lisinopril = result[0].Drugs.First(d => d.DrugName == "Lisinopril");
        Assert.Equal(0m, lisinopril.PlanCopay);
    }

    // ═══════ Helpers ═══════

    private static List<DrugPricingInput> CreateDrugInputs() =>
    [
        new("197361", "Lisinopril", "00093-7182-01", 15.50m, 8.00m, 5.00m),
        new("312961", "Metformin", "00093-1048-01", 12.00m, 6.00m, 4.00m),
    ];

    private static List<PlanDrugCoverageInput> CreateCoverages() =>
    [
        new("197361", "Lisinopril", 1, 0m, true, false),
        new("312961", "Metformin", 1, 0m, true, false),
    ];

    private void SetupPharmacies(params PharmacyWithPricing[] pharmacies)
    {
        _pharmacyServiceMock.Setup(s => s.GetPharmaciesWithPricingAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<DrugPricingInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pharmacies.ToList());
    }

    private static PharmacyWithPricing CreateCvsPharmacy(List<DrugPricingInput> drugs) =>
        CreatePharmacyWithPricing("CVS Pharmacy #1234", "COMMUNITY/RETAIL PHARMACY", drugs);

    private static PharmacyWithPricing CreateIndependentPharmacy(List<DrugPricingInput> drugs) =>
        CreatePharmacyWithPricing("Main Street Pharmacy", "INDEPENDENT", drugs);

    private static PharmacyWithPricing CreatePharmacyWithPricing(
        string name, string pharmacyType, List<DrugPricingInput> drugs)
    {
        return new PharmacyWithPricing
        {
            Pharmacy = new PharmacyResult
            {
                NPI = "1234567890",
                Name = name,
                PharmacyType = pharmacyType,
                Address = "123 Main St",
                City = "New York",
                State = "NY",
                ZipCode = "10001"
            },
            Drugs = drugs.Select(d => new DrugPrice
            {
                DrugName = d.DrugName,
                RxCui = d.RxCui,
                Ndc = d.Ndc,
                RetailPrice = d.RetailPrice,
                MedicarePrice = d.MedicarePrice,
                GenericPrice = d.GenericPrice
            }).ToList(),
            TotalRetailCost = drugs.Sum(d => d.RetailPrice ?? 0),
            TotalMedicareCost = drugs.Sum(d => d.MedicarePrice ?? 0),
            TotalGenericCost = drugs.Sum(d => d.GenericPrice ?? 0)
        };
    }
}
