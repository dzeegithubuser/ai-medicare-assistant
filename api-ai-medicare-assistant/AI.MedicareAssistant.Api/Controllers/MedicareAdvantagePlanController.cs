using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Interfaces;
using Domain.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedicareAdvantagePlanController : ControllerBase
{
    private readonly IMedicareAdvantagePlanService _maService;

    public MedicareAdvantagePlanController(IMedicareAdvantagePlanService maService)
    {
        _maService = maService;
    }

    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend(MedicareAdvantagePlanRequest request, CancellationToken ct)
    {
        var result = await _maService.RecommendAsync(request, ct);
        return Ok(result);
    }
}
