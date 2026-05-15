using Application.DTOs;
using Application.Interfaces;
using Application.Utilities;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class EndUserService : IEndUserService
{
    private readonly IUserRepository _userRepo;
    private readonly ILogger<EndUserService> _logger;

    public EndUserService(IUserRepository userRepo, ILogger<EndUserService> logger)
    {
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task<EndUserSummaryDto> CreateAsync(Guid fpUserId, CreateEndUserRequest request)
    {
        var fp = await _userRepo.GetByIdAsync(fpUserId)
            ?? throw new UnauthorizedException("Caller is not a financial planner.");
        if (fp.Role != UserRoles.FinancialPlanner)
            throw new UnauthorizedException("Caller is not a financial planner.");

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
            Role = UserRoles.User,
            FpId = fpUserId,
            IsEmailVerified = true,
            MustChangePassword = true,
            CreatedBy = fp.Email,
            ModifiedBy = fp.Email
        };

        await _userRepo.CreateAsync(user);
        _logger.LogInformation("FP {FpUserId} created end-user {UserId} ({Email})", fpUserId, user.UserId, email);

        return MapToDto(user);
    }

    private static EndUserSummaryDto MapToDto(UserDocument user) => new()
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
}
