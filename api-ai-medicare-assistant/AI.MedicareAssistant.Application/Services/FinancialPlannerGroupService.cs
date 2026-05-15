using Application.DTOs;
using Application.Interfaces;
using Application.Utilities;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class FinancialPlannerGroupService : IFinancialPlannerGroupService
{
    private readonly IFinancialPlannerGroupRepository _fpgRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRecommendationRepository _recRepo;
    private readonly ILogger<FinancialPlannerGroupService> _logger;

    public FinancialPlannerGroupService(
        IFinancialPlannerGroupRepository fpgRepo,
        IUserRepository userRepo,
        IRecommendationRepository recRepo,
        ILogger<FinancialPlannerGroupService> logger)
    {
        _fpgRepo = fpgRepo;
        _userRepo = userRepo;
        _recRepo = recRepo;
        _logger = logger;
    }

    public async Task<FpgSummaryDto?> GetGroupAsync(Guid fpgId)
    {
        // Absence of the group is part of the model — return null so the caller can
        // render an empty/placeholder state without the global error dialog firing.
        // (Edge case: legacy FPG admin whose group was deleted out of band.)
        var group = await _fpgRepo.GetByIdAsync(fpgId);
        if (group is null) return null;

        return new FpgSummaryDto
        {
            GroupId = group.GroupId,
            Name = group.Name,
            Description = group.Description,
            CreatedAt = group.CreatedAt
        };
    }

    public async Task<List<FpSummaryDto>> ListFinancialPlannersAsync(Guid fpgId)
    {
        var fps = await _userRepo.GetByFpgIdAndRoleAsync(fpgId, UserRoles.FinancialPlanner);
        return fps.Select(MapToFpSummary).ToList();
    }

    public async Task<FpSummaryDto> CreateFinancialPlannerAsync(Guid fpgId, CreateFpRequest request)
    {
        _ = await _fpgRepo.GetByIdAsync(fpgId)
            ?? throw new NotFoundException("FinancialPlannerGroup", fpgId);

        var email = request.Email.ToLowerInvariant();
        if (await _userRepo.EmailExistsAsync(email))
            throw new ConflictException("Email already registered.");

        var phone = PhoneNormalizer.NormalizeUsPhone(request.Phone);
        if (await _userRepo.PhoneExistsAsync(phone))
            throw new ConflictException("Phone number already registered.");

        var user = new UserDocument
        {
            Email = email,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = UserRoles.FinancialPlanner,
            FpgId = fpgId,
            IsEmailVerified = true,
            MustChangePassword = true,
            CreatedBy = "fpg",
            ModifiedBy = "fpg"
        };

        await _userRepo.CreateAsync(user);
        _logger.LogInformation("FPG {FpgId} created FP {UserId} ({Email})", fpgId, user.UserId, email);

        return MapToFpSummary(user);
    }

    public async Task<FpSummaryDto> UpdateFinancialPlannerAsync(Guid fpgId, Guid targetFpUserId, UpdateFpRequest request)
    {
        var fp = await GetOwnedFpOrThrowAsync(fpgId, targetFpUserId);

        var phone = PhoneNormalizer.NormalizeUsPhone(request.Phone);
        if (phone != fp.Phone && await _userRepo.PhoneExistsAsync(phone))
            throw new ConflictException("Phone number already registered.");

        fp.FirstName = request.FirstName;
        fp.LastName = request.LastName;
        fp.Phone = phone;
        fp.ModifiedBy = "fpg";

        await _userRepo.UpdateAsync(fp);
        return MapToFpSummary(fp);
    }

    public async Task DeleteFinancialPlannerAsync(Guid fpgId, Guid targetFpUserId)
    {
        var fp = await GetOwnedFpOrThrowAsync(fpgId, targetFpUserId);

        var endUsers = await _userRepo.GetByFpIdAsync(fp.UserId);
        if (endUsers.Count > 0)
        {
            throw new ConflictException(
                "Cannot delete a financial planner who still has end-users assigned. Reassign or delete the users first.");
        }

        await _userRepo.DeleteAsync(fp.UserId);
        _logger.LogInformation("FPG {FpgId} deleted FP {UserId}", fpgId, fp.UserId);
    }

    public async Task<List<EndUserSummaryDto>> ListGroupEndUsersAsync(Guid fpgId)
    {
        var endUsers = await _userRepo.GetEndUsersByFpgAsync(fpgId);
        return endUsers.Select(MapToEndUserSummary).ToList();
    }

    public async Task<List<RecommendationByUserDto>> ListGroupRecommendationsAsync(Guid fpgId)
    {
        var endUsers = await _userRepo.GetEndUsersByFpgAsync(fpgId);
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

    private async Task<UserDocument> GetOwnedFpOrThrowAsync(Guid fpgId, Guid fpUserId)
    {
        var fp = await _userRepo.GetByIdAsync(fpUserId);
        if (fp is null || fp.Role != UserRoles.FinancialPlanner || fp.FpgId != fpgId)
            throw new NotFoundException("FinancialPlanner", fpUserId);
        return fp;
    }


    private static FpSummaryDto MapToFpSummary(UserDocument user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Phone = user.Phone,
        FpgId = user.FpgId,
        MustChangePassword = user.MustChangePassword,
        CreatedAt = user.CreatedAt
    };

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
