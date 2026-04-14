using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for PrescriptionService — save, get by user, get by id, save current drugs/pharmacies/plans.
/// </summary>
public class PrescriptionServiceTests
{
    private readonly Mock<IPrescriptionDocRepository> _repoMock;
    private readonly Mock<IUserAnalysisSelectionsRepository> _selectionsMock;
    private readonly Mock<IProfileRepository> _profileMock;
    private readonly PrescriptionService _sut;

    public PrescriptionServiceTests()
    {
        _repoMock = new Mock<IPrescriptionDocRepository>();
        _selectionsMock = new Mock<IUserAnalysisSelectionsRepository>();
        _profileMock = new Mock<IProfileRepository>();
        _sut = new PrescriptionService(
            _repoMock.Object,
            _selectionsMock.Object,
            _profileMock.Object,
            Mock.Of<ILogger<PrescriptionService>>());
    }

    // ═══════ SaveAsync ═══════

    [Fact]
    public async Task Save_ValidRequest_SavesToRepo()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.SaveAsync(It.IsAny<PrescriptionDocument>()))
            .ReturnsAsync((PrescriptionDocument d) => d);

        var request = new SavePrescriptionRequest
        {
            Name = "My Prescription",
            Drugs =
            [
                new PrescriptionDrugDto
                {
                    DrugInput = "Eliquis 5mg",
                    NormalizedDrugName = "apixaban",
                    GenericName = "apixaban",
                    SelectedName = "Eliquis",
                    NameType = "Brand"
                }
            ]
        };

        var result = await _sut.SaveAsync(userId, request);

        Assert.Equal("My Prescription", result.Name);
        _repoMock.Verify(r => r.SaveAsync(It.Is<PrescriptionDocument>(d =>
            d.UserId == userId && d.Name == "My Prescription" && d.Drugs.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task Save_MapsDrugFieldsCorrectly()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.SaveAsync(It.IsAny<PrescriptionDocument>()))
            .ReturnsAsync((PrescriptionDocument d) => d);

        var request = new SavePrescriptionRequest
        {
            Name = "Test",
            Drugs =
            [
                new PrescriptionDrugDto
                {
                    DrugInput = "Metformin 500mg",
                    NormalizedDrugName = "metformin",
                    GenericName = "metformin",
                    SelectedName = "Metformin",
                    NameType = "Generic",
                    DosageForm = "tablet",
                    Strength = "500 mg",
                    Packaging = "Bottle of 60 tablets",
                    RxNormId = "6809",
                    NdcCode = "00093-7212-01",
                    TherapeuticCategory = "Antidiabetic",
                    DrugClass = "Biguanide",
                    QuantityPerMonth = 60
                }
            ]
        };

        await _sut.SaveAsync(userId, request);

        _repoMock.Verify(r => r.SaveAsync(It.Is<PrescriptionDocument>(d =>
            d.Drugs[0].NormalizedDrugName == "metformin" &&
            d.Drugs[0].DosageForm == "tablet" &&
            d.Drugs[0].RxNormId == "6809" &&
            d.Drugs[0].QuantityPerMonth == 60)), Times.Once);
    }

    // ═══════ GetByUserIdAsync ═══════

    [Fact]
    public async Task GetByUserId_ReturnsMappedList()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new List<PrescriptionDocument>
            {
                new() { UserId = userId, Name = "Rx 1", Drugs = [] },
                new() { UserId = userId, Name = "Rx 2", Drugs = [] }
            });

        var result = await _sut.GetByUserIdAsync(userId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByUserId_NoPrescriptions_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new List<PrescriptionDocument>());

        var result = await _sut.GetByUserIdAsync(userId);

        Assert.Empty(result);
    }

    // ═══════ GetByIdAsync ═══════

    [Fact]
    public async Task GetById_UnifiedDocExistsFirst_ReturnsUnifiedDoc()
    {
        var doc = new UserAnalysisSelectionsDocument
        {
            Id = "abc123",
            UserId = Guid.NewGuid(),
            Drugs = [new PrescriptionDrugDoc { NormalizedDrugName = "apixaban" }]
        };
        _selectionsMock.Setup(r => r.GetByIdAsync("abc123")).ReturnsAsync(doc);

        var result = await _sut.GetByIdAsync("abc123");

        Assert.NotNull(result);
        Assert.Single(result!.Drugs);
        _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetById_NoUnifiedDoc_FallsToPrescriptionRepo()
    {
        _selectionsMock.Setup(r => r.GetByIdAsync("rx123")).ReturnsAsync((UserAnalysisSelectionsDocument?)null);
        _repoMock.Setup(r => r.GetByIdAsync("rx123"))
            .ReturnsAsync(new PrescriptionDocument
            {
                Id = "rx123",
                UserId = Guid.NewGuid(),
                Name = "Test Rx",
                Drugs = []
            });
        _selectionsMock.Setup(r => r.GetCurrentForUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync((UserAnalysisSelectionsDocument?)null);

        var result = await _sut.GetByIdAsync("rx123");

        Assert.NotNull(result);
        Assert.Equal("Test Rx", result!.Name);
    }

    [Fact]
    public async Task GetById_NothingFound_ReturnsNull()
    {
        _selectionsMock.Setup(r => r.GetByIdAsync("xxx")).ReturnsAsync((UserAnalysisSelectionsDocument?)null);
        _repoMock.Setup(r => r.GetByIdAsync("xxx")).ReturnsAsync((PrescriptionDocument?)null);

        var result = await _sut.GetByIdAsync("xxx");

        Assert.Null(result);
    }

    // ═══════ SaveCurrentDrugsAsync ═══════

    [Fact]
    public async Task SaveCurrentDrugs_ExistingDoc_UpdatesDrugOnly()
    {
        var userId = Guid.NewGuid();
        _selectionsMock.Setup(r => r.GetCurrentForUserAsync(userId))
            .ReturnsAsync(new UserAnalysisSelectionsDocument { UserId = userId });

        var request = new SaveCurrentDrugsRequest
        {
            Drugs = [new PrescriptionDrugDto { NormalizedDrugName = "metformin" }]
        };

        await _sut.SaveCurrentDrugsAsync(userId, request);

        _selectionsMock.Verify(r => r.UpdateDrugsAsync(userId, It.Is<List<PrescriptionDrugDoc>>(d =>
            d.Count == 1 && d[0].NormalizedDrugName == "metformin")), Times.Once);
        _selectionsMock.Verify(r => r.ReplaceCurrentForUserAsync(It.IsAny<UserAnalysisSelectionsDocument>()), Times.Never);
    }

    [Fact]
    public async Task SaveCurrentDrugs_NoExistingDoc_CreatesNew()
    {
        var userId = Guid.NewGuid();
        _selectionsMock.Setup(r => r.GetCurrentForUserAsync(userId))
            .ReturnsAsync((UserAnalysisSelectionsDocument?)null);
        _selectionsMock.Setup(r => r.ReplaceCurrentForUserAsync(It.IsAny<UserAnalysisSelectionsDocument>()))
            .ReturnsAsync((UserAnalysisSelectionsDocument d) => { d.Id = "newId"; return d; });
        _profileMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((Profile?)null);

        await _sut.SaveCurrentDrugsAsync(userId, new SaveCurrentDrugsRequest
        {
            Drugs = [new PrescriptionDrugDto { NormalizedDrugName = "lisinopril" }]
        });

        _selectionsMock.Verify(r => r.ReplaceCurrentForUserAsync(It.Is<UserAnalysisSelectionsDocument>(d =>
            d.UserId == userId && d.Drugs.Count == 1)), Times.Once);
    }

    // ═══════ SaveCurrentPharmacyAsync ═══════

    [Fact]
    public async Task SaveCurrentPharmacy_DelegatesToRepo()
    {
        var userId = Guid.NewGuid();
        var request = new SaveCurrentPharmacyRequest
        {
            SelectedPharmacies =
            [
                new SelectedPharmacySnapshotDto
                {
                    PharmacyNumber = "1234",
                    PharmacyName = "CVS",
                    Address = "123 Main St",
                    Zipcode = "80113"
                }
            ]
        };

        await _sut.SaveCurrentPharmacyAsync(userId, request);

        _selectionsMock.Verify(r => r.UpdatePharmaciesAsync(userId, It.Is<List<UserAnalysisPharmacyDoc>>(p =>
            p.Count == 1 && p[0].PharmacyName == "CVS")), Times.Once);
    }

    // ═══════ SaveCurrentPlansAsync ═══════

    [Fact]
    public async Task SaveCurrentPlans_DelegatesToRepoWithSection()
    {
        var userId = Guid.NewGuid();
        var request = new SaveCurrentPlansRequest
        {
            FpActiveSection = "partd",
            SelectedPlans =
            [
                new SelectedPlanSnapshotDto
                {
                    Slot = "partd",
                    PlanId = "H1234-001",
                    PlanName = "Humana Gold"
                }
            ]
        };

        await _sut.SaveCurrentPlansAsync(userId, request);

        _selectionsMock.Verify(r => r.UpdatePlansAsync(
            userId,
            It.Is<List<UserAnalysisPlanDoc>>(p => p.Count == 1 && p[0].PlanName == "Humana Gold"),
            "partd"), Times.Once);
    }
}
