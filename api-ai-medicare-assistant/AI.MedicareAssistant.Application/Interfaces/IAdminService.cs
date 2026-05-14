using Application.DTOs;

namespace Application.Interfaces;

public interface IAdminService
{
    Task<List<FpgSummaryDto>> ListGroupsAsync();
    Task<FpgSummaryDto> CreateGroupAsync(CreateFpgRequest request);
    Task<UserSummaryDto> CreateGroupAdminUserAsync(Guid fpgId, CreateFpgAdminUserRequest request);

    /// <summary>List every user with role <c>financial_planner_group</c>, system-wide.</summary>
    Task<List<UserSummaryDto>> ListFpgAdminUsersAsync();

    /// <summary>
    /// Create an FPG-admin user without exposing the group concept. The backend auto-creates a
    /// <see cref="Domain.Documents.FinancialPlannerGroupDocument"/> derived from the user's name
    /// and assigns the new user to it.
    /// </summary>
    Task<UserSummaryDto> CreateFpgAdminUserAsync(CreateFpgAdminUserRequest request);

    /// <summary>
    /// Delete an FPG-admin user and their auto-created group. Rejects with <see cref="Domain.Exceptions.ConflictException"/>
    /// if the group still has FPs (the admin must clean up FPs first via the FPG home).
    /// </summary>
    Task DeleteFpgAdminUserAsync(Guid userId);
}
