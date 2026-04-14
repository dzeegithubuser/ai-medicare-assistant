using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Interfaces;
using Domain.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PartDPlanController : ControllerBase
{
    private readonly IPartDPlanRecommendationService _service;

    public PartDPlanController(IPartDPlanRecommendationService service)
    {
        _service = service;
    }

    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend(
        [FromBody] PartDPlanRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.RecommendAsync(request, cancellationToken);
        return Ok(result);
    }
}
