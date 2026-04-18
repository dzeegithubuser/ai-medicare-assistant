using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Domain.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlanRecommendationController : ControllerBase
{
    private readonly CostProjectionService _costProjectionService;

    public PlanRecommendationController(
        CostProjectionService costProjectionService)
    {
        _costProjectionService = costProjectionService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
