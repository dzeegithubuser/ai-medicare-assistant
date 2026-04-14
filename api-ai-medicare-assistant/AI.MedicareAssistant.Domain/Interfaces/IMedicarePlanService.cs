using Domain.Models;

namespace Domain.Interfaces;

public interface IMedicarePlanService
{
    Task<PlanRecommendationResult> RecommendPlansAsync(
        PlanRecommendationRequest request,
        CancellationToken cancellationToken = default);
}
