using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Interfaces;
using Domain.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedigapPlanController : ControllerBase
{
    private readonly IMedigapPlanQuotesService _medigapService;

    public MedigapPlanController(IMedigapPlanQuotesService medigapService)
    {
        _medigapService = medigapService;
    }

    [HttpPost("quotes")]
    public async Task<IActionResult> GetQuotes(MedigapPlanQuotesRequest request, CancellationToken ct)
    {
        var result = await _medigapService.GetQuotesAsync(request, ct);
        return Ok(result);
    }
}
