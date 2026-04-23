using System.Security.Claims;
using Application.DTOs;
using Application.Services;
using Domain.Documents;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.MedicareAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecommendationController : ControllerBase
{
    private readonly RecommendationService _service;
    private readonly ILogger<RecommendationController> _logger;

    public RecommendationController(RecommendationService service, ILogger<RecommendationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new UnauthorizedException("User identity claim is missing or invalid.");
        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var doc = await _service.GetActiveAsync(GetUserId());
        if (doc is null) return NotFound();
        return Ok(MapToResponse(doc));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var doc = await _service.GetByIdAsync(id, GetUserId());
        if (doc is null) return NotFound();
        return Ok(MapToResponse(doc));
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var docs = await _service.GetAllAsync(GetUserId());
        var summaries = docs.Select(d => new RecommendationSummaryResponse
        {
            Id = d.Id,
            Name = d.Name,
            Status = d.Status,
            Type = d.Type,
            DrugCount = d.DrugList.Count,
            PlanCount = d.PlanSelections.Count,
            HasCostSnapshot = d.LastCostSnapshot is not null || d.LtcSnapshot is not null,
            LifetimeTotal = d.Type == "longterm"
                ? d.LtcSnapshot?.TotalCost ?? 0
                : d.LastCostSnapshot?.LifetimeTotal ?? 0,
            Plans = d.PlanSelections.Select(p => new PlanSummaryItem
            {
                PlanType = p.PlanType,
                PlanName = p.PlanName
            }).ToList(),
            HealthProfile = d.LtcSnapshot?.HealthProfile,
            AdultDayYears = d.LtcSnapshot?.AdultDayYears,
            HomeCareYears = d.LtcSnapshot?.HomeCareYears,
            NursingCareYears = d.LtcSnapshot?.NursingCareYears,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        });
        return Ok(summaries);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecommendationRequest request,
        [FromQuery] bool force = false)
    {
        var document = new RecommendationDocument
        {
            Name = request.Name,
            Type = request.Type,
            Profile = MapToProfileSnapshot(request.Profile),
            DrugList = request.Drugs.Select(MapToDrugDoc).ToList(),
            Pharmacies = request.Pharmacies.Select(MapToPharmacyDoc).ToList(),
            PlanSelections = request.Plans.Select(MapToPlanDoc).ToList(),
            LastCostSnapshot = request.CostSnapshot is not null ? MapToCostSnapshotDoc(request.CostSnapshot) : null,
            LtcSnapshot = request.LtcSnapshot is not null ? MapToLtcSnapshotDoc(request.LtcSnapshot) : null
        };

        var created = await _service.CreateAsync(GetUserId(), document, force);
        return CreatedAtAction(nameof(Get), MapToResponse(created));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var profile = MapToProfileSnapshot(request.Profile);
        var updated = await _service.UpdateProfileAsync(GetUserId(), profile);
        return Ok(MapToResponse(updated));
    }

    [HttpPut("drugs")]
    public async Task<IActionResult> UpdateDrugs([FromBody] UpdateDrugsRequest request)
    {
        var drugs = request.Drugs.Select(MapToDrugDoc).ToList();
        var updated = await _service.UpdateDrugsAsync(GetUserId(), drugs);
        return Ok(MapToResponse(updated));
    }

    [HttpPut("pharmacy")]
    public async Task<IActionResult> UpdatePharmacy([FromBody] UpdatePharmacyRequest request)
    {
        var pharmacies = request.Pharmacies.Select(MapToPharmacyDoc).ToList();
        var mailOrder = request.MailOrderPharmacy is not null ? MapToMailOrderDoc(request.MailOrderPharmacy) : null;
        var updated = await _service.UpdatePharmacyAsync(GetUserId(), pharmacies, mailOrder);
        return Ok(MapToResponse(updated));
    }

    [HttpPut("plans")]
    public async Task<IActionResult> UpdatePlans([FromBody] UpdatePlansRequest request)
    {
        var plans = request.Plans.Select(MapToPlanDoc).ToList();
        var updated = await _service.UpdatePlansAsync(GetUserId(), plans);
        return Ok(MapToResponse(updated));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] bool confirmed = false)
    {
        if (!confirmed)
            return BadRequest(new { message = "Deletion requires confirmed=true query parameter." });

        await _service.DeleteAsync(GetUserId());
        return NoContent();
    }

    // ── Mapping helpers ──

    private static RecommendationResponse MapToResponse(RecommendationDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Status = doc.Status,
        Type = doc.Type,
        Profile = MapToProfileDto(doc.Profile),
        PlanSelections = doc.PlanSelections.Select(MapToPlanDto).ToList(),
        DrugList = doc.DrugList.Select(MapToDrugDto).ToList(),
        Pharmacies = doc.Pharmacies.Select(MapToPharmacyDto).ToList(),
        MailOrderPharmacy = doc.MailOrderPharmacy is not null ? MapToMailOrderDto(doc.MailOrderPharmacy) : null,
        LastCostSnapshot = doc.LastCostSnapshot is not null ? MapToCostSnapshotDto(doc.LastCostSnapshot) : null,
        LtcSnapshot = doc.LtcSnapshot is not null ? MapToLtcSnapshotDto(doc.LtcSnapshot) : null,
        CreatedAt = doc.CreatedAt,
        UpdatedAt = doc.UpdatedAt
    };

    private static ProfileSnapshot MapToProfileSnapshot(ProfileSnapshotDto dto) => new()
    {
        RecommendationName = dto.RecommendationName,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        DateOfBirth = dto.DateOfBirth,
        Gender = dto.Gender,
        ZipCode = dto.ZipCode,
        County = dto.County,
        CountyCode = dto.CountyCode,
        State = dto.State,
        City = dto.City,
        AddressLine1 = dto.AddressLine1,
        HealthCondition = dto.HealthCondition,
        LifeExpectancy = dto.LifeExpectancy,
        TobaccoStatus = dto.TobaccoStatus,
        TaxFilingStatus = dto.TaxFilingStatus,
        MagiTier = dto.MagiTier,
        CoverageYear = dto.CoverageYear,
        Concierge = dto.Concierge,
        ConciergeAmount = dto.ConciergeAmount,
        AlternateEmail = dto.AlternateEmail,
        AlternateMobile = dto.AlternateMobile,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude
    };

    private static ProfileSnapshotDto MapToProfileDto(ProfileSnapshot p) => new()
    {
        RecommendationName = p.RecommendationName,
        FirstName = p.FirstName,
        LastName = p.LastName,
        DateOfBirth = p.DateOfBirth,
        Gender = p.Gender,
        ZipCode = p.ZipCode,
        County = p.County,
        CountyCode = p.CountyCode,
        State = p.State,
        City = p.City,
        AddressLine1 = p.AddressLine1,
        HealthCondition = p.HealthCondition,
        LifeExpectancy = p.LifeExpectancy,
        TobaccoStatus = p.TobaccoStatus,
        TaxFilingStatus = p.TaxFilingStatus,
        MagiTier = p.MagiTier,
        CoverageYear = p.CoverageYear,
        Concierge = p.Concierge,
        ConciergeAmount = p.ConciergeAmount,
        AlternateEmail = p.AlternateEmail,
        AlternateMobile = p.AlternateMobile,
        Latitude = p.Latitude,
        Longitude = p.Longitude
    };

    private static SelectedDrugDoc MapToDrugDoc(SelectedDrugDto dto) => new()
    {
        DrugName = dto.DrugName,
        FullName = dto.FullName,
        DrugType = dto.DrugType,
        Dosage = dto.Dosage,
        Quantity = dto.Quantity,
        RefillFrequency = dto.RefillFrequency,
        Rxcui = dto.Rxcui,
        NdcCode = dto.NdcCode
    };

    private static SelectedDrugDto MapToDrugDto(SelectedDrugDoc d) => new()
    {
        DrugName = d.DrugName,
        FullName = d.FullName,
        DrugType = d.DrugType,
        Dosage = d.Dosage,
        Quantity = d.Quantity,
        RefillFrequency = d.RefillFrequency,
        Rxcui = d.Rxcui,
        NdcCode = d.NdcCode
    };

    private static SelectedPlanDoc MapToPlanDoc(SelectedPlanDto dto) => new()
    {
        PlanType = dto.PlanType,
        PlanId = dto.PlanId,
        PlanName = dto.PlanName,
        Carrier = dto.Carrier,
        MonthlyPremium = dto.MonthlyPremium,
        MedigapPlanType = dto.MedigapPlanType,
        Deductible = dto.Deductible,
        StarRating = dto.StarRating,
        TotalPrescriptionCost = dto.TotalPrescriptionCost,
        TotalPlanCost = dto.TotalPlanCost,
        PrescriptionDrugCovered = dto.PrescriptionDrugCovered,
        UnavailableDrugs = dto.UnavailableDrugs,
        PlanExpenses = dto.PlanExpenses.Select(e => new PlanExpenseDoc
        {
            Month = e.Month, Oop = e.Oop, Premium = e.Premium, DrugRetailCost = e.DrugRetailCost
        }).ToList()
    };

    private static SelectedPlanDto MapToPlanDto(SelectedPlanDoc d) => new()
    {
        PlanType = d.PlanType,
        PlanId = d.PlanId,
        PlanName = d.PlanName,
        Carrier = d.Carrier,
        MonthlyPremium = d.MonthlyPremium,
        MedigapPlanType = d.MedigapPlanType,
        Deductible = d.Deductible,
        StarRating = d.StarRating,
        TotalPrescriptionCost = d.TotalPrescriptionCost,
        TotalPlanCost = d.TotalPlanCost,
        PrescriptionDrugCovered = d.PrescriptionDrugCovered,
        UnavailableDrugs = d.UnavailableDrugs,
        PlanExpenses = d.PlanExpenses.Select(e => new PlanExpenseDto
        {
            Month = e.Month, Oop = e.Oop, Premium = e.Premium, DrugRetailCost = e.DrugRetailCost
        }).ToList()
    };

    private static SelectedPharmacyDoc MapToPharmacyDoc(SelectedPharmacyDto dto) => new()
    {
        Npi = dto.Npi,
        Name = dto.Name,
        Address = dto.Address,
        City = dto.City,
        State = dto.State,
        ZipCode = dto.ZipCode,
        Phone = dto.Phone,
        PharmacyType = dto.PharmacyType,
        Distance = dto.Distance
    };

    private static SelectedPharmacyDto MapToPharmacyDto(SelectedPharmacyDoc d) => new()
    {
        Npi = d.Npi,
        Name = d.Name,
        Address = d.Address,
        City = d.City,
        State = d.State,
        ZipCode = d.ZipCode,
        Phone = d.Phone,
        PharmacyType = d.PharmacyType,
        Distance = d.Distance
    };

    private static MailOrderPharmacyDoc MapToMailOrderDoc(MailOrderPharmacyDto dto) => new()
    {
        Npi = dto.Npi,
        Name = dto.Name,
        Enabled = dto.Enabled
    };

    private static MailOrderPharmacyDto MapToMailOrderDto(MailOrderPharmacyDoc d) => new()
    {
        Npi = d.Npi,
        Name = d.Name,
        Enabled = d.Enabled
    };

    private static CostSnapshotDto MapToCostSnapshotDto(CostSnapshotDoc d) => new()
    {
        LifetimeTotal = d.LifetimeTotal,
        LifetimePremiums = d.LifetimePremiums,
        LifetimeOop = d.LifetimeOop,
        LifetimeIrmaa = d.LifetimeIrmaa,
        PresentValue = d.PresentValue,
        CurrentYearTotal = d.CurrentYearTotal,
        CalculatedAt = d.CalculatedAt,
        LtcPresentValue = d.LtcPresentValue,
        SupplementPlanType = d.SupplementPlanType,
        SupplementPlanPremium = d.SupplementPlanPremium,
        YearlyDetails = d.YearlyDetails.Select(MapToYearlyDetailDto).ToList(),
        Evaluation = d.Evaluation is not null ? MapToEvaluationDto(d.Evaluation) : null
    };

    private static CostSnapshotDoc MapToCostSnapshotDoc(CostSnapshotDto dto) => new()
    {
        LifetimeTotal = dto.LifetimeTotal,
        LifetimePremiums = dto.LifetimePremiums,
        LifetimeOop = dto.LifetimeOop,
        LifetimeIrmaa = dto.LifetimeIrmaa,
        PresentValue = dto.PresentValue,
        CurrentYearTotal = dto.CurrentYearTotal,
        CalculatedAt = dto.CalculatedAt,
        LtcPresentValue = dto.LtcPresentValue,
        SupplementPlanType = dto.SupplementPlanType,
        SupplementPlanPremium = dto.SupplementPlanPremium,
        YearlyDetails = dto.YearlyDetails.Select(MapToYearlyDetailDoc).ToList(),
        Evaluation = dto.Evaluation is not null ? MapToEvaluationDoc(dto.Evaluation) : null
    };

    private static YearlyDetailDto MapToYearlyDetailDto(YearlyDetailDoc d) => new()
    {
        Year = d.Year, MonthsUsedForExpenseCalc = d.MonthsUsedForExpenseCalc,
        PartAPremium = d.PartAPremium, PartBPremium = d.PartBPremium,
        PartBPremiumSurcharge = d.PartBPremiumSurcharge,
        MedicareAdvantagePremium = d.MedicareAdvantagePremium,
        PartDPremium = d.PartDPremium, PartDPremiumSurcharge = d.PartDPremiumSurcharge,
        ConciergePremium = d.ConciergePremium,
        PartAOOP = d.PartAOOP, PartBOOP = d.PartBOOP, PartDOOP = d.PartDOOP,
        TotalABMedicareAdvantage = d.TotalABMedicareAdvantage,
        ReserveDaysLeft = d.ReserveDaysLeft,
        DentalPremium = d.DentalPremium, DentalOOP = d.DentalOOP,
        PlanGPremium = d.PlanGPremium, PlanFPremium = d.PlanFPremium, PlanNPremium = d.PlanNPremium,
        TotalABGD = d.TotalABGD, TotalABFD = d.TotalABFD, TotalABND = d.TotalABND, TotalABCD = d.TotalABCD
    };

    private static YearlyDetailDoc MapToYearlyDetailDoc(YearlyDetailDto dto) => new()
    {
        Year = dto.Year, MonthsUsedForExpenseCalc = dto.MonthsUsedForExpenseCalc,
        PartAPremium = dto.PartAPremium, PartBPremium = dto.PartBPremium,
        PartBPremiumSurcharge = dto.PartBPremiumSurcharge,
        MedicareAdvantagePremium = dto.MedicareAdvantagePremium,
        PartDPremium = dto.PartDPremium, PartDPremiumSurcharge = dto.PartDPremiumSurcharge,
        ConciergePremium = dto.ConciergePremium,
        PartAOOP = dto.PartAOOP, PartBOOP = dto.PartBOOP, PartDOOP = dto.PartDOOP,
        TotalABMedicareAdvantage = dto.TotalABMedicareAdvantage,
        ReserveDaysLeft = dto.ReserveDaysLeft,
        DentalPremium = dto.DentalPremium, DentalOOP = dto.DentalOOP,
        PlanGPremium = dto.PlanGPremium, PlanFPremium = dto.PlanFPremium, PlanNPremium = dto.PlanNPremium,
        TotalABGD = dto.TotalABGD, TotalABFD = dto.TotalABFD, TotalABND = dto.TotalABND, TotalABCD = dto.TotalABCD
    };

    private static CostEvaluationDto MapToEvaluationDto(CostEvaluationDoc d) => new()
    {
        PlanName = d.PlanName, PlanBundleCode = d.PlanBundleCode,
        CostTrajectory = d.CostTrajectory, TrajectoryExplanation = d.TrajectoryExplanation,
        OverallAssessment = d.OverallAssessment,
        LifetimeSummary = new LifetimeSummaryDto
        {
            TotalPremiums = d.LifetimeSummary.TotalPremiums,
            TotalOutOfPocket = d.LifetimeSummary.TotalOutOfPocket,
            TotalCombined = d.LifetimeSummary.TotalCombined,
            ProjectionYears = d.LifetimeSummary.ProjectionYears,
            AverageAnnualCost = d.LifetimeSummary.AverageAnnualCost
        },
        YearlyHighlights = d.YearlyHighlights.Select(h => new YearlyHighlightDto
        {
            Year = h.Year, TotalCost = h.TotalCost, Flag = h.Flag, Explanation = h.Explanation
        }).ToList(),
        Categories = d.Categories.Select(c => new CostCategoryDto
        {
            Name = c.Name, LifetimeTotal = c.LifetimeTotal, PercentOfTotal = c.PercentOfTotal,
            Trend = c.Trend, Insight = c.Insight
        }).ToList(),
        SavingsTips = d.SavingsTips.Select(s => new SavingsTipDto
        {
            Title = s.Title, Description = s.Description,
            EstimatedSavings = s.EstimatedSavings, Priority = s.Priority
        }).ToList()
    };

    private static CostEvaluationDoc MapToEvaluationDoc(CostEvaluationDto dto) => new()
    {
        PlanName = dto.PlanName, PlanBundleCode = dto.PlanBundleCode,
        CostTrajectory = dto.CostTrajectory, TrajectoryExplanation = dto.TrajectoryExplanation,
        OverallAssessment = dto.OverallAssessment,
        LifetimeSummary = new LifetimeSummaryDoc
        {
            TotalPremiums = dto.LifetimeSummary.TotalPremiums,
            TotalOutOfPocket = dto.LifetimeSummary.TotalOutOfPocket,
            TotalCombined = dto.LifetimeSummary.TotalCombined,
            ProjectionYears = dto.LifetimeSummary.ProjectionYears,
            AverageAnnualCost = dto.LifetimeSummary.AverageAnnualCost
        },
        YearlyHighlights = dto.YearlyHighlights.Select(h => new YearlyHighlightDoc
        {
            Year = h.Year, TotalCost = h.TotalCost, Flag = h.Flag, Explanation = h.Explanation
        }).ToList(),
        Categories = dto.Categories.Select(c => new CostCategoryDoc
        {
            Name = c.Name, LifetimeTotal = c.LifetimeTotal, PercentOfTotal = c.PercentOfTotal,
            Trend = c.Trend, Insight = c.Insight
        }).ToList(),
        SavingsTips = dto.SavingsTips.Select(s => new SavingsTipDoc
        {
            Title = s.Title, Description = s.Description,
            EstimatedSavings = s.EstimatedSavings, Priority = s.Priority
        }).ToList()
    };

    // ── LTC Snapshot mapping ──

    private static LtcSnapshotDto MapToLtcSnapshotDto(LtcSnapshotDoc d) => new()
    {
        HealthProfile = d.HealthProfile,
        AdultDayYears = d.AdultDayYears,
        HomeCareYears = d.HomeCareYears,
        NursingCareYears = d.NursingCareYears,
        TotalCost = d.TotalCost,
        TotalPresentValue = d.TotalPresentValue,
        Projection = d.Projection is not null ? MapToLtcProjectionDto(d.Projection) : null,
        Evaluation = d.Evaluation is not null ? MapToLtcEvaluationDto(d.Evaluation) : null
    };

    private static LtcSnapshotDoc MapToLtcSnapshotDoc(LtcSnapshotDto dto) => new()
    {
        HealthProfile = dto.HealthProfile,
        AdultDayYears = dto.AdultDayYears,
        HomeCareYears = dto.HomeCareYears,
        NursingCareYears = dto.NursingCareYears,
        TotalCost = dto.TotalCost,
        TotalPresentValue = dto.TotalPresentValue,
        Projection = dto.Projection is not null ? MapToLtcProjectionDoc(dto.Projection) : null,
        Evaluation = dto.Evaluation is not null ? MapToLtcEvaluationDoc(dto.Evaluation) : null
    };

    private static LtcProjectionDto MapToLtcProjectionDto(LtcProjectionDoc d) => new()
    {
        PvHomeCare = d.PvHomeCare,
        PvNursingCare = d.PvNursingCare,
        AdultDayExpenses = d.AdultDayExpenses.Select(e => new LtcExpenseEntryDto { Year = e.Year, Expense = e.Expense }).ToList(),
        HomeCareExpenses = d.HomeCareExpenses.Select(e => new LtcExpenseEntryDto { Year = e.Year, Expense = e.Expense }).ToList(),
        AssistedCareExpenses = d.AssistedCareExpenses.Select(e => new LtcExpenseEntryDto { Year = e.Year, Expense = e.Expense }).ToList(),
        NursingCareExpenses = d.NursingCareExpenses.Select(e => new LtcExpenseEntryDto { Year = e.Year, Expense = e.Expense }).ToList(),
    };

    private static LtcProjectionDoc MapToLtcProjectionDoc(LtcProjectionDto dto) => new()
    {
        PvHomeCare = dto.PvHomeCare,
        PvNursingCare = dto.PvNursingCare,
        AdultDayExpenses = dto.AdultDayExpenses.Select(e => new LtcExpenseEntryDoc { Year = e.Year, Expense = e.Expense }).ToList(),
        HomeCareExpenses = dto.HomeCareExpenses.Select(e => new LtcExpenseEntryDoc { Year = e.Year, Expense = e.Expense }).ToList(),
        AssistedCareExpenses = dto.AssistedCareExpenses.Select(e => new LtcExpenseEntryDoc { Year = e.Year, Expense = e.Expense }).ToList(),
        NursingCareExpenses = dto.NursingCareExpenses.Select(e => new LtcExpenseEntryDoc { Year = e.Year, Expense = e.Expense }).ToList(),
    };

    private static LtcEvaluationDto MapToLtcEvaluationDto(LtcEvaluationDoc d) => new()
    {
        CostTrajectory = d.CostTrajectory,
        TrajectoryExplanation = d.TrajectoryExplanation,
        OverallAssessment = d.OverallAssessment,
        TotalCost = d.TotalCost,
        TotalPresentValue = d.TotalPresentValue,
        ProjectionYears = d.ProjectionYears,
        AverageAnnualCost = d.AverageAnnualCost,
        YearlyHighlights = d.YearlyHighlights.Select(h => new YearlyHighlightDto
        {
            Year = h.Year, TotalCost = h.TotalCost, Flag = h.Flag, Explanation = h.Explanation
        }).ToList(),
        Categories = d.Categories.Select(c => new LtcCostCategoryDto
        {
            Name = c.Name, LifetimeTotal = c.LifetimeTotal, PresentValue = c.PresentValue,
            PercentOfTotal = c.PercentOfTotal, Trend = c.Trend, Insight = c.Insight
        }).ToList(),
        SavingsTips = d.SavingsTips.Select(s => new SavingsTipDto
        {
            Title = s.Title, Description = s.Description,
            EstimatedSavings = s.EstimatedSavings, Priority = s.Priority
        }).ToList()
    };

    private static LtcEvaluationDoc MapToLtcEvaluationDoc(LtcEvaluationDto dto) => new()
    {
        CostTrajectory = dto.CostTrajectory,
        TrajectoryExplanation = dto.TrajectoryExplanation,
        OverallAssessment = dto.OverallAssessment,
        TotalCost = dto.TotalCost,
        TotalPresentValue = dto.TotalPresentValue,
        ProjectionYears = dto.ProjectionYears,
        AverageAnnualCost = dto.AverageAnnualCost,
        YearlyHighlights = dto.YearlyHighlights.Select(h => new YearlyHighlightDoc
        {
            Year = h.Year, TotalCost = h.TotalCost, Flag = h.Flag, Explanation = h.Explanation
        }).ToList(),
        Categories = dto.Categories.Select(c => new LtcCostCategoryDoc
        {
            Name = c.Name, LifetimeTotal = c.LifetimeTotal, PresentValue = c.PresentValue,
            PercentOfTotal = c.PercentOfTotal, Trend = c.Trend, Insight = c.Insight
        }).ToList(),
        SavingsTips = dto.SavingsTips.Select(s => new SavingsTipDoc
        {
            Title = s.Title, Description = s.Description,
            EstimatedSavings = s.EstimatedSavings, Priority = s.Priority
        }).ToList()
    };
}
