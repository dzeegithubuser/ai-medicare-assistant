using Domain.Models;

namespace Domain.Interfaces;

public interface IPartDPlanRecommendationService
{
    Task<PartDPlanRecommendationResponse> RecommendAsync(
        PartDPlanRecommendationRequest request,
        CancellationToken cancellationToken = default);
}
