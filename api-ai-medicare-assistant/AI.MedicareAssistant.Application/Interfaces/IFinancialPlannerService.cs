using Application.DTOs;

namespace Application.Interfaces;

public interface IFinancialPlannerService
{
    Task<List<EndUserSummaryDto>> ListEndUsersAsync(Guid fpUserId);
    Task<List<RecommendationByUserDto>> ListRecommendationsAsync(Guid fpUserId);
    Task DeleteRecommendationAsync(Guid fpUserId, string recommendationId);

    /// <summary>
    /// Cascade-delete an end-user and every per-user document (profile, chat session,
    /// recommendations, analysis selections, LTC selections). Verifies caller owns the target.
    /// </summary>
    Task DeleteEndUserAsync(Guid fpUserId, Guid endUserId);
}
