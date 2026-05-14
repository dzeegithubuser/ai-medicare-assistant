using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AdminService : IAdminService
{
    private readonly IFinancialPlannerGroupRepository _fpgRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IFinancialPlannerGroupRepository fpgRepo,
        IUserRepository userRepo,
        ILogger<AdminService> logger)
    {
        _fpgRepo = fpgRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task<List<FpgSummaryDto>> ListGroupsAsync()
    {
        var groups = await _fpgRepo.GetAllAsync();
        return groups.Select(MapToDto).ToList();
    }

    public async Task<FpgSummaryDto> CreateGroupAsync(CreateFpgRequest request)
    {
        if (await _fpgRepo.ExistsByNameAsync(request.Name))
            throw new ConflictException($"A financial planner group named '{request.Name}' already exists.");

        var doc = new FinancialPlannerGroupDocument
        {
            Name = request.Name,
            Description = request.Description,
            CreatedBy = "admin",
            ModifiedBy = "admin"
        };

        await _fpgRepo.CreateAsync(doc);
        _logger.LogInformation("Admin created FPG {GroupId} ({Name})", doc.GroupId, doc.Name);

        return MapToDto(doc);
    }

    public async Task<UserSummaryDto> CreateGroupAdminUserAsync(Guid fpgId, CreateFpgAdminUserRequest request)
    {
        var group = await _fpgRepo.GetByIdAsync(fpgId)
            ?? throw new NotFoundException("FinancialPlannerGroup", fpgId);

        var email = request.Email.ToLowerInvariant();
        if (await _userRepo.EmailExistsAsync(email))
            throw new ConflictException("Email already registered.");

        var phone = await GenerateUniqueDummyPhoneAsync();

        var user = new UserDocument
        {
            Email = email,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = UserRoles.FinancialPlannerGroup,
            FpgId = fpgId,
            IsEmailVerified = true,
            MustChangePassword = true,
            CreatedBy = "admin",
            ModifiedBy = "admin"
        };

        await _userRepo.CreateAsync(user);
        _logger.LogInformation(
            "Admin created FPG-admin user {UserId} ({Email}) in group {GroupId} ({GroupName})",
            user.UserId, user.Email, group.GroupId, group.Name);

        return MapUserToSummary(user);
    }

    public async Task<List<UserSummaryDto>> ListFpgAdminUsersAsync()
    {
        var users = await _userRepo.GetAllByRoleAsync(UserRoles.FinancialPlannerGroup);
        return users
            .OrderByDescending(u => u.CreatedAt)
            .Select(MapUserToSummary)
            .ToList();
    }

    public async Task<UserSummaryDto> CreateFpgAdminUserAsync(CreateFpgAdminUserRequest request)
    {
        var email = request.Email.ToLowerInvariant();
        if (await _userRepo.EmailExistsAsync(email))
            throw new ConflictException("Email already registered.");

        var groupName = await GenerateUniqueGroupNameAsync(request.FirstName, request.LastName);

        var group = new FinancialPlannerGroupDocument
        {
            Name = groupName,
            Description = $"Auto-created for {request.FirstName} {request.LastName}".Trim(),
            CreatedBy = "admin",
            ModifiedBy = "admin"
        };
        await _fpgRepo.CreateAsync(group);

        var phone = await GenerateUniqueDummyPhoneAsync();

        var user = new UserDocument
        {
            Email = email,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = UserRoles.FinancialPlannerGroup,
            FpgId = group.GroupId,
            IsEmailVerified = true,
            MustChangePassword = true,
            CreatedBy = "admin",
            ModifiedBy = "admin"
        };

        await _userRepo.CreateAsync(user);
        _logger.LogInformation(
            "Admin created FPG-admin user {UserId} ({Email}); auto-group {GroupId} ({GroupName})",
            user.UserId, user.Email, group.GroupId, group.Name);

        return MapUserToSummary(user);
    }

    public async Task DeleteFpgAdminUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("FpgAdminUser", userId);
        if (user.Role != UserRoles.FinancialPlannerGroup)
            throw new UnauthorizedException("Target user is not an FPG admin.");

        if (user.FpgId.HasValue)
        {
            var fps = await _userRepo.GetByFpgIdAndRoleAsync(user.FpgId.Value, UserRoles.FinancialPlanner);
            if (fps.Count > 0)
            {
                throw new ConflictException(
                    $"Cannot remove this FPG admin while {fps.Count} financial planner(s) still belong to their group. " +
                    "Sign in as the FPG admin and remove each planner first (planners with end-users must have those end-users removed first too).");
            }

            await _fpgRepo.DeleteAsync(user.FpgId.Value);
        }

        await _userRepo.DeleteAsync(userId);
        _logger.LogInformation(
            "Admin deleted FPG-admin user {UserId} ({Email}) and their group {GroupId}",
            userId, user.Email, user.FpgId);
    }

    private async Task<string> GenerateUniqueGroupNameAsync(string firstName, string lastName)
    {
        var baseName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrEmpty(baseName))
            baseName = "Financial Planner Group";

        if (!await _fpgRepo.ExistsByNameAsync(baseName))
            return baseName;

        for (var i = 2; i <= 50; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!await _fpgRepo.ExistsByNameAsync(candidate))
                return candidate;
        }
        throw new ConflictException("Unable to allocate a unique group name; please retry.");
    }

    private async Task<string> GenerateUniqueDummyPhoneAsync()
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var suffix = Random.Shared.Next(0, 100_000).ToString("D5");
            var phone = $"55501{suffix}";
            if (!await _userRepo.PhoneExistsAsync(phone))
                return phone;
        }
        throw new ConflictException("Unable to allocate a unique dummy phone number; please retry.");
    }

    private static FpgSummaryDto MapToDto(FinancialPlannerGroupDocument doc) => new()
    {
        GroupId = doc.GroupId,
        Name = doc.Name,
        Description = doc.Description,
        CreatedAt = doc.CreatedAt
    };

    private static UserSummaryDto MapUserToSummary(UserDocument user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Phone = user.Phone,
        Role = user.Role,
        FpgId = user.FpgId,
        FpId = user.FpId,
        MustChangePassword = user.MustChangePassword,
        CreatedAt = user.CreatedAt
    };
}
