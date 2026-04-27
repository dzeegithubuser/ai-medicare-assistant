using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatIntentController : ControllerBase
{
    private readonly IChatIntentClassifier _intentClassifier;
    private readonly IProfileExtractor _profileExtractor;
    private readonly IDrugSelectionExtractor _drugSelectionExtractor;
    private readonly IPharmacySelectionExtractor _pharmacySelectionExtractor;
    private readonly IPlanSelectionExtractor _planSelectionExtractor;

    public ChatIntentController(
        IChatIntentClassifier intentClassifier,
        IProfileExtractor profileExtractor,
        IDrugSelectionExtractor drugSelectionExtractor,
        IPharmacySelectionExtractor pharmacySelectionExtractor,
        IPlanSelectionExtractor planSelectionExtractor)
    {
        _intentClassifier = intentClassifier;
        _profileExtractor = profileExtractor;
        _drugSelectionExtractor = drugSelectionExtractor;
        _pharmacySelectionExtractor = pharmacySelectionExtractor;
        _planSelectionExtractor = planSelectionExtractor;
    }

    [HttpPost("intent")]
    public async Task<IActionResult> ClassifyIntent(
        [FromBody] ChatIntentRequest request,
        CancellationToken ct)
    {
        var result = await _intentClassifier.ClassifyAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-profile")]
    public async Task<IActionResult> ExtractProfile(
        [FromBody] ProfileExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _profileExtractor.ExtractAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-drug-selection")]
    public async Task<IActionResult> ExtractDrugSelection(
        [FromBody] DrugSelectionExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _drugSelectionExtractor.ExtractAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-pharmacy-selection")]
    public async Task<IActionResult> ExtractPharmacySelection(
        [FromBody] PharmacySelectionExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _pharmacySelectionExtractor.ExtractAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("extract-plan-selection")]
    public async Task<IActionResult> ExtractPlanSelection(
        [FromBody] PlanSelectionExtractRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var result = await _planSelectionExtractor.ExtractAsync(request, ct);
        return Ok(result);
    }
}
