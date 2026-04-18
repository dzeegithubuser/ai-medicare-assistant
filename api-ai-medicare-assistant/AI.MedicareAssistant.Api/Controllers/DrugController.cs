
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

    public DrugController(IDrugAiService aiService)
    {
        _aiService = aiService;
    }

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

}

public class DrugNameRequest
{
    public required string Input { get; set; }
}
