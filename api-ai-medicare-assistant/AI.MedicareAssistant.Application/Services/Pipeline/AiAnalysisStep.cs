using System.Text.Json;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.Pipeline;

/// <summary>
/// Step 1: Calls the AI service to analyze the prescription text
/// and parses the JSON response into a DrugAnalysisResult.
/// </summary>
public class AiAnalysisStep : IDrugAnalysisStep
{
    private readonly IDrugAiService _ai;
    private readonly ILogger<AiAnalysisStep> _logger;

    public int Order => 1;

    public AiAnalysisStep(IDrugAiService ai, ILogger<AiAnalysisStep> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(DrugAnalysisResult result, AnalysisContext context)
    {
        _logger.LogInformation("Starting AI analysis (prescription length={Length})",
            context.Prescription.Length);

        var rawJson = await _ai.AnalyzePrescription(context.Prescription);

        _logger.LogDebug("AI response received ({Length} chars)", rawJson.Length);

        var parsed = JsonSerializer.Deserialize<DrugAnalysisResult>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed is null)
            return true; // empty result continues to validation which will short-circuit

        // Copy parsed data into the shared result object
        result.Drugs = parsed.Drugs;
        result.Interactions = parsed.Interactions;
        result.DosageAlerts = parsed.DosageAlerts;
        result.DuplicateTherapies = parsed.DuplicateTherapies;
        result.Message = parsed.Message;

        return true;
    }
}
