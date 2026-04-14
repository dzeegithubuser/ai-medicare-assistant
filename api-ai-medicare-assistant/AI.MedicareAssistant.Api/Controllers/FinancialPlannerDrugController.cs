using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Interfaces;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinancialPlannerDrugController : ControllerBase
{
    private readonly IFinancialPlannerDrugService _drugService;

    public FinancialPlannerDrugController(IFinancialPlannerDrugService drugService)
    {
        _drugService = drugService;
    }

    [HttpPost("search-bulk")]
    public async Task<IActionResult> SearchDrugsBulk(BulkDrugSearchInput request, CancellationToken ct)
    {
        var results = await _drugService.SearchBulkAsync(request.DrugNames, ct);
        return Ok(results);
    }
}

public class BulkDrugSearchInput
{
    public required List<string> DrugNames { get; set; }
}
