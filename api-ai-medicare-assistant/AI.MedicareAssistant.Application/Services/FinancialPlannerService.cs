using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class FinancialPlannerService : IFinancialPlannerService
{
    private readonly IUserRepository _userRepo;
    private readonly IRecommendationRepository _recRepo;
    private readonly IProfileRepository _profileRepo;
    private readonly IChatSessionRepository _chatRepo;
    private readonly IUserAnalysisSelectionsRepository _selectionsRepo;
    private readonly ILtcSelectionsRepository _ltcRepo;
    private readonly ILogger<FinancialPlannerService> _logger;

    public FinancialPlannerService(
        IUserRepository userRepo,
        IRecommendationRepository recRepo,
        IProfileRepository profileRepo,
        IChatSessionRepository chatRepo,
        IUserAnalysisSelectionsRepository selectionsRepo,
        ILtcSelectionsRepository ltcRepo,
        ILogger<FinancialPlannerService> logger)
    {
        _userRepo = userRepo;
        _recRepo = recRepo;
        _profileRepo = profileRepo;
        _chatRepo = chatRepo;
        _selectionsRepo = selectionsRepo;
        _ltcRepo = ltcRepo;
        _logger = logger;
    }

    public async Task<List<EndUserSummaryDto>> ListEndUsersAsync(Guid fpUserId)
    {
        var endUsers = await _userRepo.GetByFpIdAsync(fpUserId);
        return endUsers.Select(MapToEndUserSummary).ToList();
    }

    public async Task<List<RecommendationByUserDto>> ListRecommendationsAsync(Guid fpUserId)
    {
        var endUsers = await _userRepo.GetByFpIdAsync(fpUserId);
        if (endUsers.Count == 0) return new List<RecommendationByUserDto>();

        var userIds = endUsers.Select(u => u.UserId).ToList();
        var recs = await _recRepo.GetByUserIdsAsync(userIds);

        var byUser = recs
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).ToList());

        return endUsers
            .Where(u => byUser.ContainsKey(u.UserId))
            .Select(u => new RecommendationByUserDto
            {
                UserId = u.UserId,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Recommendations = byUser[u.UserId].Select(MapRecToSummary).ToList()
            })
            .OrderByDescending(g => g.Recommendations.First().CreatedAt)
            .ToList();
    }

    public async Task DeleteRecommendationAsync(Guid fpUserId, string recommendationId)
    {
        var rec = await _recRepo.GetByIdAsync(recommendationId)
            ?? throw new NotFoundException("Recommendation", recommendationId);

        var endUser = await _userRepo.GetByIdAsync(rec.UserId);
        if (endUser is null || endUser.Role != UserRoles.User || endUser.FpId != fpUserId)
            throw new UnauthorizedException("Recommendation does not belong to one of your end-users.");

        await _recRepo.DeleteByIdAsync(recommendationId);
        _logger.LogInformation("FP {FpUserId} deleted recommendation {RecId} for user {UserId}", fpUserId, recommendationId, rec.UserId);
    }

    public async Task DeleteEndUserAsync(Guid fpUserId, Guid endUserId)
    {
        var target = await _userRepo.GetByIdAsync(endUserId)
            ?? throw new NotFoundException("EndUser", endUserId);
        if (target.Role != UserRoles.User || target.FpId != fpUserId)
            throw new UnauthorizedException("Target is not one of your end-users.");

        // Cascade per-user collections before deleting the user itself.
        await _profileRepo.DeleteByUserIdAsync(endUserId);
        await _chatRepo.DeleteByUserIdAsync(endUserId);
        await _recRepo.DeleteByUserIdAsync(endUserId);
        await _selectionsRepo.DeleteByUserIdAsync(endUserId);
        await _ltcRepo.DeleteByUserIdAsync(endUserId);
        await _userRepo.DeleteAsync(endUserId);

        _logger.LogInformation(
            "FP {FpUserId} cascade-deleted end-user {EndUserId} ({Email})",
            fpUserId, endUserId, target.Email);
    }

    private static EndUserSummaryDto MapToEndUserSummary(UserDocument user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Phone = user.Phone,
        FpId = user.FpId,
        MustChangePassword = user.MustChangePassword,
        CreatedAt = user.CreatedAt
    };

    private static RecommendationSummaryDto MapRecToSummary(RecommendationDocument rec) => new()
    {
        Id = rec.Id,
        Name = rec.Name,
        Status = rec.Status,
        Type = rec.Type,
        CreatedAt = rec.CreatedAt,
        UpdatedAt = rec.UpdatedAt
    };
}
