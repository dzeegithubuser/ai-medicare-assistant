using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.Pipeline;

/// <summary>
/// Step 2: Filters out invalid/unrecognizable drug entries from the AI response.
/// Short-circuits the pipeline if no valid drugs remain.
/// </summary>
public class DrugValidationStep : IDrugAnalysisStep
{
    private readonly ILogger<DrugValidationStep> _logger;

    public int Order => 2;

    public DrugValidationStep(ILogger<DrugValidationStep> logger)
    {
        _logger = logger;
    }

    public Task<bool> ExecuteAsync(DrugAnalysisResult result, AnalysisContext context)
    {
        // Populate flat arrays from formulations for backward compatibility
        foreach (var drug in result.Drugs)
        {
            if (drug.Formulations.Count > 0)
            {
                if (drug.DosageForms.Count == 0)
                    drug.DosageForms = drug.Formulations.Select(f => f.DosageForm).Distinct().ToList();
                if (drug.Strengths.Count == 0)
                    drug.Strengths = drug.Formulations.Select(f => f.Strength).Distinct().ToList();
                if (drug.Packaging.Count == 0)
                    drug.Packaging = drug.Formulations.Select(f => f.Packaging).Distinct().ToList();
                if (drug.NdcCodes.Count == 0)
                    drug.NdcCodes = drug.Formulations.Select(f => f.NdcCode).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            }
        }

        result.Drugs = result.Drugs
            .Where(d => !string.IsNullOrWhiteSpace(d.NormalizedDrugName)
                     && !string.IsNullOrWhiteSpace(d.GenericName)
                     && d.DosageForms.Count > 0
                     && (d.Formulations.Count > 0 || d.Strengths.Count > 0))
            .ToList();

        if (result.Drugs.Count == 0)
        {
            _logger.LogWarning("No valid drugs found for prescription input: {Prescription}",
                context.Prescription);
            result.Message = "No valid drugs could be identified from your input. " +
                             "Please check the spelling and try again with a valid drug name (e.g. 'Eliquis 5mg').";
            return Task.FromResult(false); // short-circuit pipeline
        }

        result.ZipCode = context.ZipCode;
        return Task.FromResult(true);
    }
}
