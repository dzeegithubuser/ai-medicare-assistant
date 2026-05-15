using Application.DTOs;

namespace Application.Interfaces;

public interface IFinancialPlannerGroupService
{
    /// <summary>
    /// Returns the caller's group, or <c>null</c> when no group exists for that <c>fpgId</c>.
    /// Surfaced via <c>GET /api/financial-planner-group/me</c> as a 200 OK with null body so
    /// callers don't have to treat "group missing" as an error condition.
    /// </summary>
    Task<FpgSummaryDto?> GetGroupAsync(Guid fpgId);
    Task<List<FpSummaryDto>> ListFinancialPlannersAsync(Guid fpgId);
    Task<FpSummaryDto> CreateFinancialPlannerAsync(Guid fpgId, CreateFpRequest request);
    Task<FpSummaryDto> UpdateFinancialPlannerAsync(Guid fpgId, Guid targetFpUserId, UpdateFpRequest request);
    Task DeleteFinancialPlannerAsync(Guid fpgId, Guid targetFpUserId);
    Task<List<EndUserSummaryDto>> ListGroupEndUsersAsync(Guid fpgId);
    Task<List<RecommendationByUserDto>> ListGroupRecommendationsAsync(Guid fpgId);
}
