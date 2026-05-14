using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/impersonate")]
[Authorize]
public class ImpersonationController : ControllerBase
{
    private readonly IImpersonationService _impersonation;

    public ImpersonationController(IImpersonationService impersonation) =>
        _impersonation = impersonation;

    /// <summary>FP starts impersonating one of their end-users. Requires the FP role.</summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.FinancialPlanner)]
    public async Task<ActionResult<ImpersonationResponse>> Start([FromBody] ImpersonateRequest request)
    {
        var fpUserId = GetUserId();
        return Ok(await _impersonation.ImpersonateAsync(fpUserId, request.TargetUserId));
    }

    /// <summary>
    /// Refreshes an active impersonation token (used by the 55-min "continue?" prompt).
    /// Accepts the impersonation token (Role=user) and reads the FP id from the <c>actingAs</c> claim.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ImpersonationResponse>> Refresh()
    {
        var actingAs = User.FindFirstValue("actingAs");
        if (string.IsNullOrEmpty(actingAs) || !Guid.TryParse(actingAs, out var fpUserId))
            throw new UnauthorizedException("Not currently impersonating.");

        var targetUserId = GetUserId();
        return Ok(await _impersonation.ImpersonateAsync(fpUserId, targetUserId));
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(claim, out var id)) return id;
        throw new UnauthorizedException("Missing user id claim.");
    }
}
