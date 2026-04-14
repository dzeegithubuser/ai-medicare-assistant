using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Domain.Interfaces;
using Domain.Models.Pharmacy;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PharmacyController : ControllerBase
{
    private readonly IPharmacyPricingService _pharmacyService;
    private readonly IPlanPharmacyService _planPharmacyService;
    private readonly IPharmacyLookupService _pharmacyLookupService;
    private readonly ProfileService _profileService;

    public PharmacyController(
        IPharmacyPricingService pharmacyService,
        IPlanPharmacyService planPharmacyService,
        IPharmacyLookupService pharmacyLookupService,
        ProfileService profileService)
    {
        _pharmacyService = pharmacyService;
        _planPharmacyService = planPharmacyService;
        _pharmacyLookupService = pharmacyLookupService;
        _profileService = profileService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Search nearby pharmacies without pricing (location/identity only).
    /// GET /api/pharmacy/nearby?zip=90210
    /// </summary>
    [HttpGet("nearby")]
    public async Task<IActionResult> Nearby(
        [FromQuery] string? zip,
        CancellationToken cancellationToken)
    {
        var zipCode = zip;
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            var userId = GetUserId();
            var profile = await _profileService.GetProfileAsync(userId);
            zipCode = profile.Profile?.ZipCode;
        }

        if (string.IsNullOrWhiteSpace(zipCode))
            return BadRequest(new { message = "Zip code is required. Provide it via query parameter or complete your address profile." });

        var result = await _pharmacyService.GetNearbyPharmaciesAsync(zipCode, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Search nearby pharmacies with drug pricing.
    /// GET /api/pharmacy/search?zip=90210&amp;drugs=rxcui1,rxcui2
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? zip,
        [FromQuery] string? drugs,
        CancellationToken cancellationToken)
    {
        // Fall back to user's saved zip if not provided
        var zipCode = zip;
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            var userId = GetUserId();
            var profile = await _profileService.GetProfileAsync(userId);
            zipCode = profile.Profile?.ZipCode;
        }

        if (string.IsNullOrWhiteSpace(zipCode))
            return BadRequest(new { message = "Zip code is required. Provide it via query parameter or complete your address profile." });

        var rxCuis = (drugs ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (rxCuis.Count == 0)
            return BadRequest(new { message = "At least one drug RxCUI is required." });

        var drugInputs = rxCuis.Select(rxCui => new DrugPricingInput(
            RxCui: rxCui,
            DrugName: rxCui,
            Ndc: null,
            RetailPrice: null,
            MedicarePrice: null,
            GenericPrice: null
        ));

        var result = await _pharmacyService.GetPharmaciesWithPricingAsync(zipCode, drugInputs, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Plan-aware pharmacy search — overlays formulary copays and preferred network status.
    /// POST /api/pharmacy/plan-search
    /// </summary>
    [HttpPost("plan-search")]
    public async Task<IActionResult> PlanSearch(
        [FromBody] PlanPharmacySearchDto dto,
        CancellationToken cancellationToken)
    {
        // Fall back to user's saved zip if not provided
        var zipCode = dto.ZipCode;
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            var userId = GetUserId();
            var profile = await _profileService.GetProfileAsync(userId);
            zipCode = profile.Profile?.ZipCode;
        }

        if (string.IsNullOrWhiteSpace(zipCode))
            return BadRequest(new { message = "Zip code is required." });

        if (string.IsNullOrWhiteSpace(dto.PlanId))
            return BadRequest(new { message = "Plan ID is required." });

        if (dto.Drugs.Count == 0)
            return BadRequest(new { message = "At least one drug is required." });

        var drugInputs = dto.Drugs.Select(d => new DrugPricingInput(
            RxCui: d.RxCui,
            DrugName: d.DrugName,
            Ndc: null,
            RetailPrice: null,
            MedicarePrice: null,
            GenericPrice: null
        ));

        var coverages = dto.PlanCoverages.Select(c => new PlanDrugCoverageInput(
            RxCui: c.RxCui,
            DrugName: c.DrugName,
            FormularyTier: c.FormularyTier,
            MonthlyCopay: c.MonthlyCopay,
            IsCovered: c.IsCovered,
            RequiresPriorAuth: c.RequiresPriorAuth
        ));

        var result = await _planPharmacyService.GetPlanPharmaciesAsync(
            dto.PlanId, zipCode, drugInputs, coverages, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Lookup pharmacies near the user's location via Financial Planner API.
    /// GET /api/pharmacy/lookup?page=1&amp;size=20&amp;radius=25&amp;name=CVS
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] double radius = 25,
        [FromQuery] string? name = "",
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var profile = await _profileService.GetProfileAsync(userId);

        if (profile.Profile?.Latitude is null || profile.Profile?.Longitude is null)
            return BadRequest(new { message = "Profile latitude and longitude are required. Please complete your address profile." });

        var request = new PharmacyLookupRequest
        {
            Latitude = profile.Profile.Latitude.Value,
            Longitude = profile.Profile.Longitude.Value,
            SearchRadiusInMiles = radius,
            PharmacyName = name ?? "",
            Page = page,
            Size = size
        };

        var result = await _pharmacyLookupService.GetPharmaciesAsync(request, cancellationToken);
        return Ok(result);
    }
}

// ── DTOs ──

public class PlanPharmacySearchDto
{
    [JsonPropertyName("planId")]
    public string PlanId { get; set; } = "";

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }

    [JsonPropertyName("drugs")]
    public List<PlanDrugDto> Drugs { get; set; } = [];

    [JsonPropertyName("planCoverages")]
    public List<PlanCoverageDto> PlanCoverages { get; set; } = [];
}

public class PlanDrugDto
{
    [JsonPropertyName("rxCui")]
    public string RxCui { get; set; } = "";

    [JsonPropertyName("drugName")]
    public string DrugName { get; set; } = "";
}

public class PlanCoverageDto
{
    [JsonPropertyName("rxCui")]
    public string RxCui { get; set; } = "";

    [JsonPropertyName("drugName")]
    public string DrugName { get; set; } = "";

    [JsonPropertyName("formularyTier")]
    public int FormularyTier { get; set; }

    [JsonPropertyName("monthlyCopay")]
    public decimal MonthlyCopay { get; set; }

    [JsonPropertyName("isCovered")]
    public bool IsCovered { get; set; }

    [JsonPropertyName("requiresPriorAuth")]
    public bool RequiresPriorAuth { get; set; }
}
