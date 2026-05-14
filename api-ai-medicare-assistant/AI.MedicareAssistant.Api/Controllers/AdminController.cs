using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    [HttpGet("financial-planner-groups")]
    public async Task<ActionResult<List<FpgSummaryDto>>> ListGroups() =>
        Ok(await _admin.ListGroupsAsync());

    [HttpPost("financial-planner-groups")]
    public async Task<ActionResult<FpgSummaryDto>> CreateGroup([FromBody] CreateFpgRequest request) =>
        Ok(await _admin.CreateGroupAsync(request));

    [HttpPost("financial-planner-groups/{fpgId}/admin-user")]
    public async Task<ActionResult<UserSummaryDto>> CreateGroupAdminUser(
        [FromRoute] Guid fpgId, [FromBody] CreateFpgAdminUserRequest request) =>
        Ok(await _admin.CreateGroupAdminUserAsync(fpgId, request));

    [HttpGet("fpg-admin-users")]
    public async Task<ActionResult<List<UserSummaryDto>>> ListFpgAdminUsers() =>
        Ok(await _admin.ListFpgAdminUsersAsync());

    [HttpPost("fpg-admin-users")]
    public async Task<ActionResult<UserSummaryDto>> CreateFpgAdminUser(
        [FromBody] CreateFpgAdminUserRequest request) =>
        Ok(await _admin.CreateFpgAdminUserAsync(request));

    [HttpDelete("fpg-admin-users/{userId}")]
    public async Task<IActionResult> DeleteFpgAdminUser([FromRoute] Guid userId)
    {
        await _admin.DeleteFpgAdminUserAsync(userId);
        return NoContent();
    }
}
