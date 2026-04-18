using Application.DTOs;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class PrescriptionService
{
    private readonly IUserAnalysisSelectionsRepository _selectionsRepo;
    private readonly IProfileRepository _profileRepo;
    private readonly ILogger<PrescriptionService> _logger;

    public PrescriptionService(
        IUserAnalysisSelectionsRepository selectionsRepo,
        IProfileRepository profileRepo,
        ILogger<PrescriptionService> logger)
    {
        _selectionsRepo = selectionsRepo;
        _profileRepo = profileRepo;
        _logger = logger;
    }

    /// <summary>
    /// Upserts one document in <c>userAnalysisSelections</c> with drugs + pharmacies + plans (same model).
    /// Removes legacy <c>__current_prescriptions__</c> row if present. Profile id points at this document.
    /// </summary>
    public async Task<PrescriptionResponse> SaveCurrentAsync(Guid userId, SaveCurrentPrescriptionsRequest request)
    {
        _logger.LogInformation(
            "Upserting current FP analysis ({DrugCount} drugs, pharmacies={PhCount}, plans={PlCount}) for user {UserId}",
            request.Drugs.Count, request.SelectedPharmacies.Count, request.SelectedPlans.Count, userId);

        var unified = new UserAnalysisSelectionsDocument
        {
            UserId = userId,
            Drugs = request.Drugs.Select(MapDrugDtoToDoc).ToList(),
            FpActiveSection = request.FpActiveSection,
            SelectedPharmacies = request.SelectedPharmacies.Select(MapPharmacyDtoToUserDoc).ToList(),
            SelectedPlans = request.SelectedPlans.Select(MapPlanSnapshotDtoToUserDoc).ToList()
        };

        var saved = await _selectionsRepo.ReplaceCurrentForUserAsync(unified);

        var profile = await _profileRepo.GetByUserIdAsync(userId);
        if (profile is not null)
        {
            profile.CurrentPrescriptionDocumentId = saved.Id;
            await _profileRepo.UpdateAsync(profile);
        }

        return MapUnifiedDocumentToResponse(saved);
    }

    private static PrescriptionDrugDoc MapDrugDtoToDoc(PrescriptionDrugDto d) => new()
    {
        DrugInput = d.DrugInput,
        NormalizedDrugName = d.NormalizedDrugName,
        GenericName = d.GenericName,
        SelectedName = d.SelectedName,
        NameType = d.NameType,
        DosageForm = d.DosageForm,
        Strength = d.Strength,
        Packaging = d.Packaging,
        RxNormId = d.RxNormId,
        NdcCode = d.NdcCode,
        TherapeuticCategory = d.TherapeuticCategory,
        DrugClass = d.DrugClass,
        QuantityPerMonth = d.QuantityPerMonth
    };

    private static UserAnalysisPharmacyDoc MapPharmacyDtoToUserDoc(SelectedPharmacySnapshotDto p) => new()
    {
        PharmacyNumber = p.PharmacyNumber,
        PharmacyName = p.PharmacyName,
        Address = p.Address,
        Distance = p.Distance,
        Zipcode = p.Zipcode
    };

    private static UserAnalysisPlanDoc MapPlanSnapshotDtoToUserDoc(SelectedPlanSnapshotDto p) => new()
    {
        Slot = p.Slot,
        PlanId = p.PlanId,
        PlanName = p.PlanName,
        ContractId = p.ContractId,
        MedigapKey = p.MedigapKey,
        MedigapPlanType = p.MedigapPlanType,
        CompanyName = p.CompanyName
    };

    /// <summary>
    /// Replaces only drugs in <c>userAnalysisSelections</c>. Pharmacies and plans are untouched.
    /// If no current document exists yet, creates one with just the drugs (first-time user).
    /// </summary>
    public async Task SaveCurrentDrugsAsync(Guid userId, SaveCurrentDrugsRequest request)
    {
        _logger.LogInformation("Saving current drugs ({Count}) for user {UserId}", request.Drugs.Count, userId);

        var existing = await _selectionsRepo.GetCurrentForUserAsync(userId);
        if (existing is null)
        {
            // First-time: create the document with drugs and empty pharmacies/plans.
            var doc = new UserAnalysisSelectionsDocument
            {
                UserId = userId,
                Drugs = request.Drugs.Select(MapDrugDtoToDoc).ToList()
            };
            var saved = await _selectionsRepo.ReplaceCurrentForUserAsync(doc);
            var profile = await _profileRepo.GetByUserIdAsync(userId);
            if (profile is not null)
            {
                profile.CurrentPrescriptionDocumentId = saved.Id;
                await _profileRepo.UpdateAsync(profile);
            }
        }
        else
        {
            await _selectionsRepo.UpdateDrugsAsync(userId, request.Drugs.Select(MapDrugDtoToDoc).ToList());
        }
    }

    /// <summary>
    /// Replaces only pharmacies in <c>userAnalysisSelections</c>. Drugs and plans are untouched.
    /// </summary>
    public async Task SaveCurrentPharmacyAsync(Guid userId, SaveCurrentPharmacyRequest request)
    {
        _logger.LogInformation("Saving current pharmacies ({Count}) for user {UserId}", request.SelectedPharmacies.Count, userId);
        await _selectionsRepo.UpdatePharmaciesAsync(userId, request.SelectedPharmacies.Select(MapPharmacyDtoToUserDoc).ToList());
    }

    /// <summary>
    /// Replaces only plans + fpActiveSection in <c>userAnalysisSelections</c>. Drugs and pharmacies are untouched.
    /// </summary>
    public async Task SaveCurrentPlansAsync(Guid userId, SaveCurrentPlansRequest request)
    {
        _logger.LogInformation("Saving current plans ({Count}, section={Section}) for user {UserId}",
            request.SelectedPlans.Count, request.FpActiveSection, userId);
        await _selectionsRepo.UpdatePlansAsync(
            userId,
            request.SelectedPlans.Select(MapPlanSnapshotDtoToUserDoc).ToList(),
            request.FpActiveSection);
    }

    public async Task<PrescriptionResponse?> GetByIdAsync(string id)
    {
        var unified = await _selectionsRepo.GetByIdAsync(id);
        return unified is not null ? MapUnifiedDocumentToResponse(unified) : null;
    }

    private static PrescriptionResponse MapUnifiedDocumentToResponse(UserAnalysisSelectionsDocument u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        CreatedDate = u.CreatedAt,
        Drugs = u.Drugs.Select(MapDrugDocToDto).ToList(),
        SelectedPharmacies = u.SelectedPharmacies.Select(x => new SelectedPharmacySnapshotDto
        {
            PharmacyNumber = x.PharmacyNumber,
            PharmacyName = x.PharmacyName,
            Address = x.Address,
            Distance = x.Distance,
            Zipcode = x.Zipcode
        }).ToList(),
        SelectedPlans = u.SelectedPlans.Select(x => new SelectedPlanSnapshotDto
        {
            Slot = x.Slot,
            PlanId = x.PlanId,
            PlanName = x.PlanName,
            ContractId = x.ContractId,
            MedigapKey = x.MedigapKey,
            MedigapPlanType = x.MedigapPlanType,
            CompanyName = x.CompanyName
        }).ToList(),
        FpActiveSection = u.FpActiveSection
    };

    private static PrescriptionDrugDto MapDrugDocToDto(PrescriptionDrugDoc d) => new()
    {
        DrugInput = d.DrugInput,
        NormalizedDrugName = d.NormalizedDrugName,
        GenericName = d.GenericName,
        SelectedName = d.SelectedName,
        NameType = d.NameType,
        DosageForm = d.DosageForm,
        Strength = d.Strength,
        Packaging = d.Packaging,
        RxNormId = d.RxNormId,
        NdcCode = d.NdcCode,
        TherapeuticCategory = d.TherapeuticCategory,
        DrugClass = d.DrugClass,
        QuantityPerMonth = d.QuantityPerMonth
    };

}
