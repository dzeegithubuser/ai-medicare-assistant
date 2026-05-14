using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class EndUserService : IEndUserService
{
    /// <summary>Default password every FP-created end-user starts with.</summary>
    public const string DefaultPassword = "Aivante@1234";

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

        var phone = await GenerateUniqueDummyPhoneAsync();

        var user = new UserDocument
        {
            Email = email,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
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
