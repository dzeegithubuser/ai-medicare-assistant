using Domain.Models;

namespace Domain.Interfaces;

public interface IMedicareAdvantagePlanService
{
    Task<PartDPlanRecommendationResponse> RecommendAsync(
        MedicareAdvantagePlanRequest request,
        CancellationToken cancellationToken = default);
}
