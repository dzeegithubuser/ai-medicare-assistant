using Domain.Models;

namespace Domain.Interfaces;

public interface ICostEvaluationAiService
{
    /// <summary>
    /// Calls the AI model to evaluate year-by-year Medicare cost data
    /// and produce chart-ready insights, category breakdowns, and savings tips.
    /// </summary>
    Task<CostEvaluation> EvaluateAsync(
        IndividualMedicareResponse calcResult,
        string planName,
        string planBundleCode,
        int coverageYear,
        int lifeExpectancy,
        string taxFilingStatus,
        string stateName,
        string supplementPlanType,
        decimal supplementPlanPremium,
        CancellationToken cancellationToken = default);
}
