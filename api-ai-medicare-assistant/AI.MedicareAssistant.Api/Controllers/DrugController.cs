
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Interfaces;
using Domain.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DrugController : ControllerBase
{
    private readonly IDrugAiService _aiService;
    private readonly ILogger<DrugController> _logger;

    public DrugController(IDrugAiService aiService, ILogger<DrugController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("suggest-names")]
    public async Task<IActionResult> SuggestNames(DrugNameRequest req)
    {
        _logger.LogInformation("Drug name suggestion requested for input: {Input}", req.Input);

        var rawJson = await _aiService.SuggestDrugNames(req.Input);

        var result = JsonSerializer.Deserialize<DrugNameSuggestionResult>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result is null)
            _logger.LogWarning("AI returned null/unparseable drug name suggestions for input: {Input}", req.Input);

        return Ok(result ?? new DrugNameSuggestionResult());
    }

}

public class DrugNameRequest
{
    public required string Input { get; set; }
}
