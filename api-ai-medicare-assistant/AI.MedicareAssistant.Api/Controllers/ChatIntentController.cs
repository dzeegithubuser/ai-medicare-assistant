using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatIntentController : ControllerBase
{
    private readonly ChatIntentService _intentService;
    private readonly ProfileExtractService _profileExtractService;
    private readonly DrugSelectionExtractService _drugSelectionService;
    private readonly PharmacySelectionExtractService _pharmacySelectionService;
    private readonly PlanSelectionExtractService _planSelectionService;

    public ChatIntentController(
        ChatIntentService intentService,
        ProfileExtractService profileExtractService,
        DrugSelectionExtractService drugSelectionService,
        PharmacySelectionExtractService pharmacySelectionService,
        PlanSelectionExtractService planSelectionService)
    {
        _intentService = intentService;
        _profileExtractService = profileExtractService;
        _drugSelectionService = drugSelectionService;
        _pharmacySelectionService = pharmacySelectionService;
        _planSelectionService = planSelectionService;
    }

    [HttpPost("intent")]
    public async Task<IActionResult> ClassifyIntent(
        [FromBody] ChatIntentRequest request,
        CancellationToken ct)
    {
        var result = await _intentService.ClassifyAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-profile")]
    public async Task<IActionResult> ExtractProfile(
        [FromBody] ProfileExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _profileExtractService.ExtractAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-drug-selection")]
    public async Task<IActionResult> ExtractDrugSelection(
        [FromBody] DrugSelectionExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _drugSelectionService.ExtractAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-pharmacy-selection")]
    public async Task<IActionResult> ExtractPharmacySelection(
        [FromBody] PharmacySelectionExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _pharmacySelectionService.ExtractAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-plan-selection")]
    public async Task<IActionResult> ExtractPlanSelection(
        [FromBody] PlanSelectionExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _planSelectionService.ExtractAsync(request, ct);
        return Ok(result);
    }
}
