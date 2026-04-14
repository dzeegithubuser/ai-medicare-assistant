using Domain.Models;

namespace Domain.Interfaces;

public interface IPlanScoringAiService
{
    /// <summary>
    /// Calls the AI model to generate ranked Medicare plan recommendations
    /// with per-drug formulary coverage and plain-language explanations.
    /// </summary>
    Task<PlanRecommendationResult> ScorePlansAsync(
        PlanRecommendationRequest request,
        string countyName,
        CancellationToken cancellationToken = default);
}
