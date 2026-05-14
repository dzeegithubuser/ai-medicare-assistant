using Application.DTOs;

namespace Application.Interfaces;

public interface IFinancialPlannerGroupService
{
    Task<FpgSummaryDto> GetGroupAsync(Guid fpgId);
    Task<List<FpSummaryDto>> ListFinancialPlannersAsync(Guid fpgId);
    Task<FpSummaryDto> CreateFinancialPlannerAsync(Guid fpgId, CreateFpRequest request);
    Task<FpSummaryDto> UpdateFinancialPlannerAsync(Guid fpgId, Guid targetFpUserId, UpdateFpRequest request);
    Task DeleteFinancialPlannerAsync(Guid fpgId, Guid targetFpUserId);
    Task<List<EndUserSummaryDto>> ListGroupEndUsersAsync(Guid fpgId);
    Task<List<RecommendationByUserDto>> ListGroupRecommendationsAsync(Guid fpgId);
}
