using Domain.Models;

namespace Domain.Interfaces;

public interface ILtcEvaluationAiService
{
    /// <summary>
    /// Calls the AI model to evaluate LTC cost projection data
    /// and produce chart-ready insights, category breakdowns, and savings tips.
    /// </summary>
    Task<LtcCostEvaluation> EvaluateAsync(
        LongTermCareResponse projection,
        int age,
        string state,
        int lifeExpectancy,
        int healthProfile,
        string gender,
        CancellationToken cancellationToken = default);
}
