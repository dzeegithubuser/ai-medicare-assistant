using Application.Services.Pipeline;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Thin orchestrator that runs DrugAnalysisResult through a sequence of pipeline steps.
/// Each step has a single responsibility (AI call, validation, enrichment, etc.).
/// </summary>
public class DrugAnalysisService
{
    private readonly IEnumerable<IDrugAnalysisStep> _steps;
    private readonly ILogger<DrugAnalysisService> _logger;

    public DrugAnalysisService(
        IEnumerable<IDrugAnalysisStep> steps,
        ILogger<DrugAnalysisService> logger)
    {
        _steps = steps;
        _logger = logger;
    }

    public async Task<DrugAnalysisResult> Analyze(string prescription, string? zipCode = null)
    {
        _logger.LogInformation("Starting drug analysis pipeline (length={Length}, zipCode={ZipCode})",
            prescription.Length, zipCode ?? "N/A");

        var result = new DrugAnalysisResult();
        var context = new AnalysisContext(prescription, zipCode);

        foreach (var step in _steps.OrderBy(s => s.Order))
        {
            var shouldContinue = await step.ExecuteAsync(result, context);
            if (!shouldContinue)
            {
                _logger.LogInformation("Pipeline short-circuited at {Step}", step.GetType().Name);
                break;
            }
        }

        _logger.LogInformation(
            "Drug analysis complete — {DrugCount} drug(s), {InteractionCount} interaction(s), " +
            "{AlertCount} dosage alert(s), {DuplicateCount} duplicate(s)",
            result.Drugs.Count, result.Interactions.Count,
            result.DosageAlerts.Count, result.DuplicateTherapies.Count);

        return result;
    }
}
