using System.Security.Claims;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.MedicareAssistant.Api.Controllers;

[ApiController]
[Route("api/long-term-care")]
[Authorize]
public class LongTermCareController : ControllerBase
{
    private readonly ILongTermCareService _ltcService;
    private readonly ILtcEvaluationAiService _ltcEvaluationAiService;

    public LongTermCareController(
        ILongTermCareService ltcService,
        ILtcEvaluationAiService ltcEvaluationAiService)
    {
        _ltcService = ltcService;
        _ltcEvaluationAiService = ltcEvaluationAiService;
    }

    private string GetUserEmail() =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? User.FindFirstValue("email")
        ?? User.Identity?.Name
        ?? "";

    /// <summary>Calculate LTC cost projection via the Financial Planner API + AI evaluation.</summary>
    [HttpPost]
    public async Task<IActionResult> GetProjection(
        [FromBody] LongTermCareRequest request,
        CancellationToken cancellationToken)
    {
        var userEmail = GetUserEmail();
        var projection = await _ltcService.GetProjectionAsync(request, userEmail, cancellationToken);

        var evaluation = await _ltcEvaluationAiService.EvaluateAsync(
            projection,
            request.Age,
            projection.State ?? request.Location,
            request.LifeExpectancy,
            request.HealthProfile,
            request.Gender,
            cancellationToken);

        var result = new LtcProjectionResult
        {
            Projection = projection,
            Evaluation = evaluation
        };

        return Ok(result);
    }
}
