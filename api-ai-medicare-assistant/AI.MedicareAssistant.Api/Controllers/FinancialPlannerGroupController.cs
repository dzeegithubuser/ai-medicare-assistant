using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/financial-planner-group")]
[Authorize(Roles = UserRoles.FinancialPlannerGroup)]
public class FinancialPlannerGroupController : ControllerBase
{
    private readonly IFinancialPlannerGroupService _fpg;

    public FinancialPlannerGroupController(IFinancialPlannerGroupService fpg) => _fpg = fpg;

    private Guid CallerFpgId
    {
        get
        {
            var claim = User.FindFirstValue("fpgId");
            if (Guid.TryParse(claim, out var id)) return id;
            throw new UnauthorizedException("Missing or invalid fpgId claim.");
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<FpgSummaryDto?>> GetMyGroup() =>
        Ok(await _fpg.GetGroupAsync(CallerFpgId));

    [HttpGet("me/financial-planners")]
    public async Task<ActionResult<List<FpSummaryDto>>> ListFinancialPlanners() =>
        Ok(await _fpg.ListFinancialPlannersAsync(CallerFpgId));

    [HttpPost("me/financial-planners")]
    public async Task<ActionResult<FpSummaryDto>> CreateFinancialPlanner([FromBody] CreateFpRequest request) =>
        Ok(await _fpg.CreateFinancialPlannerAsync(CallerFpgId, request));

    [HttpPut("me/financial-planners/{fpUserId}")]
    public async Task<ActionResult<FpSummaryDto>> UpdateFinancialPlanner(
        [FromRoute] Guid fpUserId, [FromBody] UpdateFpRequest request) =>
        Ok(await _fpg.UpdateFinancialPlannerAsync(CallerFpgId, fpUserId, request));

    [HttpDelete("me/financial-planners/{fpUserId}")]
    public async Task<IActionResult> DeleteFinancialPlanner([FromRoute] Guid fpUserId)
    {
        await _fpg.DeleteFinancialPlannerAsync(CallerFpgId, fpUserId);
        return NoContent();
    }

    [HttpGet("me/end-users")]
    public async Task<ActionResult<List<EndUserSummaryDto>>> ListGroupEndUsers() =>
        Ok(await _fpg.ListGroupEndUsersAsync(CallerFpgId));

    [HttpGet("me/recommendations")]
    public async Task<ActionResult<List<RecommendationByUserDto>>> ListGroupRecommendations() =>
        Ok(await _fpg.ListGroupRecommendationsAsync(CallerFpgId));
}
