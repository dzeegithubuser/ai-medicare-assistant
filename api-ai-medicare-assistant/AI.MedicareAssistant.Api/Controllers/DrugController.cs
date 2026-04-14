
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Domain.Interfaces;
using Domain.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DrugController : ControllerBase
{
    private readonly DrugAnalysisService _service;
    private readonly ProfileService _profileService;
    private readonly IDrugAiService _aiService;

    public DrugController(DrugAnalysisService service, ProfileService profileService, IDrugAiService aiService)
    {
        _service = service;
        _profileService = profileService;
        _aiService = aiService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("suggest-names")]
    public async Task<IActionResult> SuggestNames(DrugNameRequest req)
    {
        var rawJson = await _aiService.SuggestDrugNames(req.Input);

        var result = JsonSerializer.Deserialize<DrugNameSuggestionResult>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return Ok(result ?? new DrugNameSuggestionResult());
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(PrescriptionRequest req)
    {
        var userId = GetUserId();
        var profile = await _profileService.GetProfileAsync(userId);
        var zipCode = profile.Profile?.ZipCode;

        var result = await _service.Analyze(req.Prescription, zipCode);
        return Ok(result);
    }
}

public class DrugNameRequest
{
    public required string Input { get; set; }
}

public class PrescriptionRequest
{
    public required string Prescription { get; set; }
}
