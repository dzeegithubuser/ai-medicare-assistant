using Domain.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace AI.MedicareAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountyLookupController : ControllerBase
{
    private readonly ICountyLookupService _countyService;
    private readonly IConfiguration _configuration;
    private readonly IConstantsService _constantsService;

    public CountyLookupController(
        ICountyLookupService countyService,
        IConfiguration configuration,
        IConstantsService constantsService)
    {
        _countyService = countyService;
        _configuration = configuration;
        _constantsService = constantsService;
    }

    /// <summary>
    /// Returns city/county list for a given ZIP code.
    /// </summary>
    [HttpPost("getCountycodeList")]
    public async Task<IActionResult> GetCountyCodeList([FromBody] ZipCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Zipcode))
            return BadRequest(new { message = "Zipcode is required" });

        var results = await _countyService.GetCountyCodeList(request.Zipcode);
        return Ok(results);
    }

    /// <summary>
    /// Returns MAGI tier options as key-value pairs for a given filing status and coverage year.
    /// Looks up by label (JOINTLY_INCOME_STATUS or INDIVIDUAL_INCOME_STATUS) and year field.
    /// </summary>
    [HttpGet("constants/magi-tiers")]
    public async Task<IActionResult> GetMagiTiers(
        [FromQuery] string filingStatus,
        [FromQuery] int coverageYear)
    {
        if (string.IsNullOrWhiteSpace(filingStatus))
            return BadRequest(new { message = "filingStatus is required" });

        var label = filingStatus switch
        {
            "MARRIED_FILING_JOINTLY" => "JOINTLY_INCOME_STATUS",
            "FILING_INDIVIDUALLY" => "INDIVIDUAL_INCOME_STATUS",
            _ => ""
        };

        if (string.IsNullOrEmpty(label))
            return BadRequest(new { message = "Invalid filing status" });

        var items = await _constantsService.GetByFilterAsync(c =>
            c.Label.Equals(label, StringComparison.OrdinalIgnoreCase)
            && c.Year == coverageYear);

        var match = items.FirstOrDefault();
        var tiers = match?.GetValuePairs() ?? [];
        return Ok(tiers);
    }
}

public class ZipCodeRequest
{
    public string Zipcode { get; set; } = "";
}
