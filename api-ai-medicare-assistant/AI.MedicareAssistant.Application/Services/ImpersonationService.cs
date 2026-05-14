using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Documents;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class ImpersonationService : IImpersonationService
{
    private static readonly TimeSpan ImpersonationTtl = TimeSpan.FromMinutes(60);

    private readonly IUserRepository _userRepo;
    private readonly IJwtTokenIssuer _jwt;
    private readonly ILogger<ImpersonationService> _logger;

    public ImpersonationService(
        IUserRepository userRepo,
        IJwtTokenIssuer jwt,
        ILogger<ImpersonationService> logger)
    {
        _userRepo = userRepo;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<ImpersonationResponse> ImpersonateAsync(Guid fpUserId, Guid targetUserId)
    {
        var fp = await _userRepo.GetByIdAsync(fpUserId)
            ?? throw new UnauthorizedException("Caller not found.");
        if (fp.Role != UserRoles.FinancialPlanner)
            throw new UnauthorizedException("Caller is not a financial planner.");

        var target = await _userRepo.GetByIdAsync(targetUserId)
            ?? throw new NotFoundException("User", targetUserId);
        if (target.Role != UserRoles.User || target.FpId != fpUserId)
            throw new UnauthorizedException("Target is not one of your end-users.");

        // Override MustChangePassword on the impersonation token: the FP is acting on
        // the user's behalf and shouldn't be blocked by the user's first-login flag.
        var impersonatedView = new UserDocument
        {
            UserId = target.UserId,
            Email = target.Email,
            Phone = target.Phone,
            Role = target.Role,
            FpId = target.FpId,
            FpgId = target.FpgId,
            MustChangePassword = false
        };

        var (token, expiresAt) = _jwt.Issue(impersonatedView, actingAs: fpUserId, ttl: ImpersonationTtl);

        _logger.LogInformation(
            "FP {FpUserId} ({FpEmail}) issued impersonation token for end-user {TargetUserId} ({TargetEmail}); expires {ExpiresAt:O}",
            fpUserId, fp.Email, target.UserId, target.Email, expiresAt);

        return new ImpersonationResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            ActingAsUserId = fpUserId,
            TargetUserId = target.UserId,
            TargetEmail = target.Email,
            TargetFirstName = target.FirstName,
            TargetLastName = target.LastName
        };
    }
}
