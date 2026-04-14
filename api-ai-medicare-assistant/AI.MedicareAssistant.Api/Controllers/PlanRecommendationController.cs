using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Application.Services;
using Domain.Interfaces;
using Domain.Models;
using Infrastructure.AI;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlanRecommendationController : ControllerBase
{
    private readonly MedicarePlanService _planService;
    private readonly IChatClient _chatClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly ProfileService _profileService;
    private readonly IIndividualMedicareService _individualMedicareService;
    private readonly CostProjectionService _costProjectionService;

    public PlanRecommendationController(
        MedicarePlanService planService,
        IChatClient chatClient,
        PromptBuilder promptBuilder,
        ProfileService profileService,
        IIndividualMedicareService individualMedicareService,
        CostProjectionService costProjectionService)
    {
        _planService = planService;
        _chatClient = chatClient;
        _promptBuilder = promptBuilder;
        _profileService = profileService;
        _individualMedicareService = individualMedicareService;
        _costProjectionService = costProjectionService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Recommend Medicare plans based on user's drugs, income, health, and location.
    /// POST /api/plan-recommendation
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RecommendPlans(
        [FromBody] PlanRecommendationRequestDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var drugSummaries = dto.Drugs.Select(d => new DrugSummary(
            RxCui: d.RxCui,
            DrugName: d.DrugName,
            GenericName: d.GenericName,
            SelectedName: d.SelectedName,
            NameType: d.NameType,
            TherapeuticCategory: d.TherapeuticCategory,
            DrugClass: d.DrugClass,
            DosageForm: d.DosageForm,
            Strength: d.Strength,
            Packaging: d.Packaging,
            Ndc: d.Ndc
        )).ToList();

        if (drugSummaries.Count == 0)
            return BadRequest(new { message = "At least one drug is required." });

        var selectedPharmacies = dto.SelectedPharmacies?
            .Take(5)
            .Select(p => new SelectedPharmacy(p.Npi, p.Name, p.PharmacyType))
            .ToList();

        var request = await _planService.BuildRequestAsync(userId, drugSummaries,
            selectedPharmacies,
            cancellationToken);

        if (request is null)
            return BadRequest(new { message = "Please complete your profile (address and income) before requesting plan recommendations." });

        var result = await _planService.RecommendPlansAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Quick LIS eligibility check based on saved income profile.
    /// GET /api/plan-recommendation/lis-check
    /// </summary>
    [HttpGet("lis-check")]
    public async Task<IActionResult> LisCheck()
    {
        var userId = GetUserId();
        var (eligible, tier) = await _planService.CheckLisEligibilityAsync(userId);

        return Ok(new
        {
            lisEligible = eligible,
            lisTier = tier.ToString()
        });
    }

    /// <summary>
    /// AI-powered gap coverage advice for plans with missing coverages.
    /// POST /api/plan-recommendation/gap-advice
    /// </summary>
    [HttpPost("gap-advice")]
    public async Task<IActionResult> GapAdvice(
        [FromBody] GapAdviceRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.PlanName) || string.IsNullOrWhiteSpace(dto.PlanType))
            return BadRequest(new { message = "Plan name and type are required." });

        if (dto.MissingCoverages is not { Count: > 0 })
            return BadRequest(new { message = "At least one missing coverage must be specified." });

        var (systemPrompt, userPrompt) = _promptBuilder.BuildGapCoverage(new Dictionary<string, string>
        {
            ["{{PLAN_NAME}}"] = dto.PlanName,
            ["{{PLAN_TYPE}}"] = dto.PlanType,
            ["{{MISSING_COVERAGES}}"] = string.Join("\n", dto.MissingCoverages.Select(c => $"- {c}"))
        });

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var raw = response.Text?.Trim() ?? "{}";

        // Strip markdown code fences if present
        if (raw.StartsWith("```"))
        {
            var firstNewline = raw.IndexOf('\n');
            var lastFence = raw.LastIndexOf("```");
            if (firstNewline >= 0 && lastFence > firstNewline)
                raw = raw[(firstNewline + 1)..lastFence].Trim();
        }

        var parsed = JsonSerializer.Deserialize<GapCoverageResult>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return Ok(parsed ?? new GapCoverageResult());
    }

    /// <summary>
    /// Calculate costs then run AI evaluation to produce chart-ready projections.
    /// POST /api/plan-recommendation/evaluate-costs
    /// </summary>
    [HttpPost("evaluate-costs")]
    public async Task<IActionResult> EvaluateCosts(
        [FromBody] CalculateCostsRequestDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var result = await _costProjectionService.EvaluateCostsAsync(
            userId, userEmail, MapToInput(dto), cancellationToken);
        return Ok(result);
    }

    private static CostCalculationInput MapToInput(CalculateCostsRequestDto dto) => new()
    {
        PlanBundleCode = dto.PlanBundleCode,
        MedicareAdvantagePremium = dto.MedicareAdvantagePremium,
        MaWithPrescriptionBenefit = dto.MaWithPrescriptionBenefit,
        PartDOOP = dto.PartDOOP,
        PartDOOPFullYear = dto.PartDOOPFullYear,
        PartABenefitServiceCost = dto.PartABenefitServiceCost,
        PartBBenefitServiceCost = dto.PartBBenefitServiceCost,
        PlanRecommendName = dto.PlanRecommendName,
        RecommendationListId = dto.RecommendationListId,
        SupplementDataProvided = dto.SupplementDataProvided,
        PartDDataProvided = dto.PartDDataProvided,
        ReserveDaysUsed = dto.ReserveDaysUsed,
        Dental = dto.Dental,
        DentalHealthGrade = dto.DentalHealthGrade,
        BoughtPlanA = dto.BoughtPlanA,
        MedicareAdvantageDataProvided = dto.MedicareAdvantageDataProvided,
        PartDPremium = dto.PartDPremium,
        CalculateForAdjustedMonth = dto.CalculateForAdjustedMonth,
        SupplementPlanType = dto.SupplementPlanType,
    };
}

public class PlanRecommendationRequestDto
{
    [JsonPropertyName("prescriptionName")]
    public string? PrescriptionName { get; set; }

    [JsonPropertyName("drugs")]
    public List<DrugSummaryDto> Drugs { get; set; } = [];

    [JsonPropertyName("selectedPharmacies")]
    public List<SelectedPharmacyDto>? SelectedPharmacies { get; set; }
}

public class SelectedPharmacyDto
{
    [JsonPropertyName("npi")]
    public string Npi { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("pharmacyType")]
    public string PharmacyType { get; set; } = "";
}

public class DrugSummaryDto
{
    [JsonPropertyName("rxCui")]
    public string RxCui { get; set; } = "";

    [JsonPropertyName("drugName")]
    public string DrugName { get; set; } = "";

    [JsonPropertyName("genericName")]
    public string GenericName { get; set; } = "";

    [JsonPropertyName("selectedName")]
    public string? SelectedName { get; set; }

    [JsonPropertyName("nameType")]
    public string? NameType { get; set; }

    [JsonPropertyName("therapeuticCategory")]
    public string? TherapeuticCategory { get; set; }

    [JsonPropertyName("drugClass")]
    public string? DrugClass { get; set; }

    [JsonPropertyName("dosageForm")]
    public string? DosageForm { get; set; }

    [JsonPropertyName("strength")]
    public string? Strength { get; set; }

    [JsonPropertyName("packaging")]
    public string? Packaging { get; set; }

    [JsonPropertyName("ndc")]
    public string? Ndc { get; set; }
}

public class GapAdviceRequestDto
{
    [JsonPropertyName("planId")]
    public string PlanId { get; set; } = "";

    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = "";

    [JsonPropertyName("planType")]
    public string PlanType { get; set; } = "";

    [JsonPropertyName("missingCoverages")]
    public List<string> MissingCoverages { get; set; } = [];
}

public class GapCoverageResult
{
    [JsonPropertyName("gapPlans")]
    public List<GapPlanDto> GapPlans { get; set; } = [];

    [JsonPropertyName("comparisonTip")]
    public string ComparisonTip { get; set; } = "";
}

public class GapPlanDto
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = "";

    [JsonPropertyName("planType")]
    public string PlanType { get; set; } = "";

    [JsonPropertyName("carrier")]
    public string Carrier { get; set; } = "";

    [JsonPropertyName("monthlyPremiumRange")]
    public string MonthlyPremiumRange { get; set; } = "";

    [JsonPropertyName("annualDeductible")]
    public string AnnualDeductible { get; set; } = "";

    [JsonPropertyName("coverageHighlights")]
    public List<string> CoverageHighlights { get; set; } = [];

    [JsonPropertyName("whyNeeded")]
    public string WhyNeeded { get; set; } = "";

    [JsonPropertyName("enrollmentTip")]
    public string EnrollmentTip { get; set; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "";
}

public class CalculateCostsRequestDto
{
    [JsonPropertyName("planBundleCode")]
    public string PlanBundleCode { get; set; } = "";

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

    [JsonPropertyName("planRecommendName")]
    public string PlanRecommendName { get; set; } = "";

    [JsonPropertyName("recommendationListId")]
    public string RecommendationListId { get; set; } = "";

    [JsonPropertyName("supplementDataProvided")]
    public bool SupplementDataProvided { get; set; }

    [JsonPropertyName("partDDataProvided")]
    public bool PartDDataProvided { get; set; }

    [JsonPropertyName("reserveDaysUsed")]
    public int ReserveDaysUsed { get; set; }

    [JsonPropertyName("dental")]
    public bool Dental { get; set; }

    [JsonPropertyName("dentalHealthGrade")]
    public int DentalHealthGrade { get; set; }

    [JsonPropertyName("boughtPlanA")]
    public bool BoughtPlanA { get; set; }

    [JsonPropertyName("medicareAdvantageDataProvided")]
    public bool MedicareAdvantageDataProvided { get; set; }

    [JsonPropertyName("partDPremium")]
    public decimal PartDPremium { get; set; }

    [JsonPropertyName("calculateForAdjustedMonth")]
    public int CalculateForAdjustedMonth { get; set; }

    [JsonPropertyName("supplementPlanType")]
    public string SupplementPlanType { get; set; } = "";
}
