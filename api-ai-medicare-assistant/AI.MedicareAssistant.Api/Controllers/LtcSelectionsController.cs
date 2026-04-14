using System.Security.Claims;
using Application.DTOs;
using Domain.Interfaces;
using Domain.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/ltc")]
[Authorize]
public class LtcSelectionsController : ControllerBase
{
    private readonly ILtcSelectionsRepository _repo;

    public LtcSelectionsController(ILtcSelectionsRepository repo)
    {
        _repo = repo;
    }

    [HttpPut("current")]
    public async Task<IActionResult> SaveCurrent([FromBody] SaveLtcCurrentRequest request)
    {
        var userId = GetUserId();
        var doc = new LtcCurrentSelectionsDocument
        {
            UserId = userId,
            HealthProfile = request.HealthProfile,
            NumberOfAdultDayHealthCareYears = request.NumberOfAdultDayHealthCareYears,
            NumberOfHomeCareYears = request.NumberOfHomeCareYears,
            NumberOfNursingCareYears = request.NumberOfNursingCareYears,
            LtcResultJson = request.LtcResultJson
        };
        await _repo.UpsertCurrentAsync(doc);
        return NoContent();
    }

    [HttpGet("current")]
    public async Task<ActionResult<LtcCurrentResponse>> GetCurrent()
    {
        var userId = GetUserId();
        var doc = await _repo.GetCurrentAsync(userId);
        if (doc == null) return NotFound();

        return Ok(new LtcCurrentResponse
        {
            HealthProfile = doc.HealthProfile,
            NumberOfAdultDayHealthCareYears = doc.NumberOfAdultDayHealthCareYears,
            NumberOfHomeCareYears = doc.NumberOfHomeCareYears,
            NumberOfNursingCareYears = doc.NumberOfNursingCareYears,
            LtcResultJson = doc.LtcResultJson,
            UpdatedAt = doc.UpdatedAt
        });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
