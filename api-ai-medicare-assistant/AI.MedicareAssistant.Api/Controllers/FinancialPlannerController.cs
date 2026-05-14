using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/financial-planner")]
[Authorize(Roles = UserRoles.FinancialPlanner)]
public class FinancialPlannerController : ControllerBase
{
    private readonly IFinancialPlannerService _fp;
    private readonly IEndUserService _endUsers;

    public FinancialPlannerController(IFinancialPlannerService fp, IEndUserService endUsers)
    {
        _fp = fp;
        _endUsers = endUsers;
    }

    private Guid CallerUserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(claim, out var id)) return id;
            throw new UnauthorizedException("Missing or invalid user id claim.");
        }
    }

    [HttpGet("me/end-users")]
    public async Task<ActionResult<List<EndUserSummaryDto>>> ListEndUsers() =>
        Ok(await _fp.ListEndUsersAsync(CallerUserId));

    [HttpPost("me/end-users")]
    public async Task<ActionResult<EndUserSummaryDto>> CreateEndUser([FromBody] CreateEndUserRequest request) =>
        Ok(await _endUsers.CreateAsync(CallerUserId, request));

    [HttpGet("me/recommendations")]
    public async Task<ActionResult<List<RecommendationByUserDto>>> ListRecommendations() =>
        Ok(await _fp.ListRecommendationsAsync(CallerUserId));

    [HttpDelete("me/recommendations/{recommendationId}")]
    public async Task<IActionResult> DeleteRecommendation([FromRoute] string recommendationId)
    {
        await _fp.DeleteRecommendationAsync(CallerUserId, recommendationId);
        return NoContent();
    }

    [HttpDelete("me/end-users/{endUserId}")]
    public async Task<IActionResult> DeleteEndUser([FromRoute] Guid endUserId)
    {
        await _fp.DeleteEndUserAsync(CallerUserId, endUserId);
        return NoContent();
    }
}
