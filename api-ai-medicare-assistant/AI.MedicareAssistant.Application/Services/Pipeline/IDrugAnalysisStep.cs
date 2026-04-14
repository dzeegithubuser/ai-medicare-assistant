using Domain.Models;

namespace Application.Services.Pipeline;

/// <summary>
/// A single step in the drug analysis pipeline.
/// Each step receives the current result and analysis context,
/// and mutates the result in place.
/// </summary>
public interface IDrugAnalysisStep
{
    /// <summary>Order in which this step runs (lower = earlier).</summary>
    int Order { get; }

    /// <summary>
    /// Returns true if the pipeline should continue after this step.
    /// Returning false short-circuits remaining steps (e.g. validation failure).
    /// </summary>
    Task<bool> ExecuteAsync(DrugAnalysisResult result, AnalysisContext context);
}

/// <summary>
/// Immutable context passed through the pipeline.
/// Carries the original inputs that steps may need.
/// </summary>
public record AnalysisContext(
    string Prescription,
    string? ZipCode
);
