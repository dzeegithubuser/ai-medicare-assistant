using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for PrescriptionService — get by id, save current drugs/pharmacies/plans.
/// </summary>
public class PrescriptionServiceTests
{
    private readonly Mock<IUserAnalysisSelectionsRepository> _selectionsMock;
    private readonly Mock<IProfileRepository> _profileMock;
    private readonly PrescriptionService _sut;

    public PrescriptionServiceTests()
    {
        _selectionsMock = new Mock<IUserAnalysisSelectionsRepository>();
        _profileMock = new Mock<IProfileRepository>();
        _sut = new PrescriptionService(
            _selectionsMock.Object,
            _profileMock.Object,
            Mock.Of<ILogger<PrescriptionService>>());
    }

    // ═══════ GetByIdAsync ═══════

    [Fact]
    public async Task GetById_UnifiedDocExistsFirst_ReturnsUnifiedDoc()
    {
        var userId = Guid.NewGuid();
        _selectionsMock.Setup(r => r.GetByIdAsync("abc123"))
            .ReturnsAsync(new UserAnalysisSelectionsDocument
            {
                Id = "abc123",
                UserId = userId,
                Drugs = [new PrescriptionDrugDoc { DrugInput = "Eliquis 5mg" }]
            });

        var result = await _sut.GetByIdAsync("abc123");

        Assert.NotNull(result);
        Assert.Single(result!.Drugs);
        _selectionsMock.Verify(r => r.GetByIdAsync("abc123"), Times.Once);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNull()
    {
        _selectionsMock.Setup(r => r.GetByIdAsync("xxx")).ReturnsAsync((UserAnalysisSelectionsDocument?)null);

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
            Drugs = [new PrescriptionDrugDto { DrugInput = "Metformin 500mg", NormalizedDrugName = "metformin" }]
        };

        await _sut.SaveCurrentDrugsAsync(userId, request);

        _selectionsMock.Verify(r => r.UpdateDrugsAsync(
            userId,
            It.Is<List<PrescriptionDrugDoc>>(d => d.Count == 1 && d[0].NormalizedDrugName == "metformin")),
            Times.Once);
        _selectionsMock.Verify(r => r.ReplaceCurrentForUserAsync(It.IsAny<UserAnalysisSelectionsDocument>()), Times.Never);
    }

    [Fact]
    public async Task SaveCurrentDrugs_NoExistingDoc_CreatesNew()
    {
        var userId = Guid.NewGuid();
        _selectionsMock.Setup(r => r.GetCurrentForUserAsync(userId))
            .ReturnsAsync((UserAnalysisSelectionsDocument?)null);
        _selectionsMock.Setup(r => r.ReplaceCurrentForUserAsync(It.IsAny<UserAnalysisSelectionsDocument>()))
            .ReturnsAsync((UserAnalysisSelectionsDocument d) => d);
        _profileMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((ProfileDocument?)null);

        var request = new SaveCurrentDrugsRequest
        {
            Drugs = [new PrescriptionDrugDto { DrugInput = "Lisinopril 10mg", NormalizedDrugName = "lisinopril" }]
        };

        await _sut.SaveCurrentDrugsAsync(userId, request);

        _selectionsMock.Verify(r => r.ReplaceCurrentForUserAsync(
            It.Is<UserAnalysisSelectionsDocument>(d => d.UserId == userId && d.Drugs.Count == 1)),
            Times.Once);
        _selectionsMock.Verify(r => r.UpdateDrugsAsync(It.IsAny<Guid>(), It.IsAny<List<PrescriptionDrugDoc>>()), Times.Never);
    }

    // ═══════ SaveCurrentPharmacyAsync ═══════

    [Fact]
    public async Task SaveCurrentPharmacy_DelegatesToRepo()
    {
        var userId = Guid.NewGuid();
        var request = new SaveCurrentPharmacyRequest
        {
            SelectedPharmacies = [new SelectedPharmacySnapshotDto { PharmacyName = "CVS", PharmacyNumber = "P001" }]
        };

        await _sut.SaveCurrentPharmacyAsync(userId, request);

        _selectionsMock.Verify(r => r.UpdatePharmaciesAsync(
            userId,
            It.Is<List<UserAnalysisPharmacyDoc>>(p => p.Count == 1 && p[0].PharmacyName == "CVS")),
            Times.Once);
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