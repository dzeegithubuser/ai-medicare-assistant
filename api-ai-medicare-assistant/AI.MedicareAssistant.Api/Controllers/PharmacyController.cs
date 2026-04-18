using System.Security.Claims;
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
    private readonly IPharmacyLookupService _pharmacyLookupService;
    private readonly ProfileService _profileService;

    public PharmacyController(
        IPharmacyLookupService pharmacyLookupService,
        ProfileService profileService)
    {
        _pharmacyLookupService = pharmacyLookupService;
        _profileService = profileService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
