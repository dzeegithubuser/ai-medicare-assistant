using Application.DTOs;
using Domain.Documents;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Computes before/after cost deltas when a recommendation field changes.
/// Uses the existing CostProjectionService to recalculate, then diffs against the stored snapshot.
/// </summary>
public class DeltaCalculationService
{
    private readonly CostProjectionService _costProjection;
    private readonly RecommendationService _recommendation;
    private readonly IChatClient _chatClient;
    private readonly ILogger<DeltaCalculationService> _logger;

    private static readonly string NarrativePromptPath =
        Path.Combine("Prompts", "system", "delta-narrative-system.txt");
    private readonly string _narrativeSystemPrompt;

    public DeltaCalculationService(
        CostProjectionService costProjection,
        RecommendationService recommendation,
        IChatClient chatClient,
        ILogger<DeltaCalculationService> logger)
    {
        _costProjection = costProjection;
        _recommendation = recommendation;
        _chatClient = chatClient;
        _logger = logger;
        _narrativeSystemPrompt = File.ReadAllText(NarrativePromptPath);
    }

    /// <summary>
    /// Compute cost delta for a proposed change.
    /// Takes the current recommendation snapshot as "before" and recalculates costs for the proposed state.
    /// </summary>
    public async Task<DeltaResult> ComputeAsync(
        Guid userId,
        string userEmail,
        RecommendationDocument currentRec,
        CostCalculationInput proposedInput,
        string fieldChanged,
        string previousValue,
        string newValue,
        CancellationToken ct = default)
    {
        var before = currentRec.LastCostSnapshot
            ?? throw new InvalidOperationException("Cannot compute delta without an existing cost snapshot.");

        // Step 1: Run Financial Planner calculation for proposed state
        CostProjectionResult afterResult;
        try
        {
            afterResult = await _costProjection.EvaluateCostsAsync(userId, userEmail, proposedInput, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cost recalculation failed for delta computation (user {UserId})", userId);
            throw;
        }

        // Step 2: Extract "after" totals from the new calculation
        var afterSnapshot = BuildSnapshotFromResult(afterResult);

        // Step 3: Build delta
        var delta = new DeltaResult
        {
            PreviousLifetimeTotal = before.LifetimeTotal,
            UpdatedLifetimeTotal = afterSnapshot.LifetimeTotal,
            PreviousCurrentYearTotal = before.CurrentYearTotal,
            UpdatedCurrentYearTotal = afterSnapshot.CurrentYearTotal,
            PreviousPresentValue = before.PresentValue,
            UpdatedPresentValue = afterSnapshot.PresentValue,
            FieldChanged = fieldChanged,
            PreviousValue = previousValue,
            NewValue = newValue,
            LtcPresentValueDelta = (afterSnapshot.LtcPresentValue ?? 0m) - (before.LtcPresentValue ?? 0m)
        };

        // Step 4: Generate narrative summary via AI
        delta.NarrativeSummary = await GenerateNarrativeAsync(delta, ct);

        return delta;
    }

    /// <summary>
    /// Builds a lightweight delta using only the stored snapshot (no recalculation).
    /// Useful for non-cost-impacting changes or when a full recalculation isn't needed yet.
    /// </summary>
    public DeltaResult BuildPreviewDelta(
        CostSnapshotDoc snapshot,
        string fieldChanged,
        string previousValue,
        string newValue)
    {
        return new DeltaResult
        {
            PreviousLifetimeTotal = snapshot.LifetimeTotal,
            UpdatedLifetimeTotal = snapshot.LifetimeTotal, // no change
            PreviousCurrentYearTotal = snapshot.CurrentYearTotal,
            UpdatedCurrentYearTotal = snapshot.CurrentYearTotal,
            PreviousPresentValue = snapshot.PresentValue,
            UpdatedPresentValue = snapshot.PresentValue,
            FieldChanged = fieldChanged,
            PreviousValue = previousValue,
            NewValue = newValue,
            NarrativeSummary = $"Changing **{fieldChanged}** from \"{previousValue}\" to \"{newValue}\". This change does not directly affect cost projections."
        };
    }

    /// <summary>
    /// Build a CostSnapshotDoc from a fresh CostProjectionResult for storage.
    /// </summary>
    public static CostSnapshotDoc BuildSnapshotFromResult(CostProjectionResult result)
    {
        var totals = result.LifetimeTotals;

        // Use plan-specific lifetime totals based on supplement type, falling back to MA
        var suppType = (totals.SupplementPlanType ?? "").ToUpperInvariant();
        var lifetimeTotal = suppType switch
        {
            "G" or "HDG" => totals.LifeTimeABGDExpenses,
            "F" or "HDF" => totals.LifeTimeABFDExpenses,
            "N" or "HDN" => totals.LifeTimeABNDExpenses,
            "C" => totals.LifeTimeABCDExpenses,
            _ => totals.LifeTimeABMedicareAdvantageExpenses
        };
        var premiums = suppType switch
        {
            "G" or "HDG" => totals.LifeTimeABGDPremium,
            "F" or "HDF" => totals.LifeTimeABFDPremium,
            "N" or "HDN" => totals.LifeTimeABNDPremium,
            "C" => totals.LifeTimeABCDPremium,
            _ => totals.LifeTimeABMedicareAdvantagePremium
        };
        var oop = suppType switch
        {
            "G" or "HDG" => totals.LifeTimeABGDOop,
            "F" or "HDF" => totals.LifeTimeABFDOop,
            "N" or "HDN" => totals.LifeTimeABNDOop,
            "C" => totals.LifeTimeABCDOop,
            _ => totals.LifeTimeABMedicareAdvantageOop
        };
        var irmaa = totals.TotalIrmaa;

        // Current year total = first year detail total if available
        var currentYearTotal = 0m;
        if (result.YearlyDetails.Count > 0)
        {
            var first = result.YearlyDetails[0];
            currentYearTotal = first.PartAPremium + first.PartBPremium + first.PartBPremiumSurcharge
                + first.PartDPremium + first.PartDPremiumSurcharge + first.PartAOOP
                + first.PartBOOP + first.PartDOOP + first.ConciergePremium
                + first.MedicareAdvantagePremium + first.DentalPremium + first.DentalOOP;
        }

        // Use real FP present value, fall back to evaluation summary
        var presentValue = result.PresentValue > 0
            ? result.PresentValue
            : result.Evaluation?.LifetimeSummary?.TotalCombined ?? lifetimeTotal;

        return new CostSnapshotDoc
        {
            LifetimeTotal = lifetimeTotal,
            LifetimePremiums = premiums,
            LifetimeOop = oop,
            LifetimeIrmaa = irmaa,
            CurrentYearTotal = currentYearTotal,
            PresentValue = presentValue,
            SupplementPlanType = totals.SupplementPlanType,
            SupplementPlanPremium = totals.SupplementPlanPremium,
            CalculatedAt = DateTime.UtcNow
        };
    }

    private async Task<string> GenerateNarrativeAsync(DeltaResult delta, CancellationToken ct)
    {
        var lifetimeDiff = delta.UpdatedLifetimeTotal - delta.PreviousLifetimeTotal;
        var yearDiff = delta.UpdatedCurrentYearTotal - delta.PreviousCurrentYearTotal;
        var direction = lifetimeDiff >= 0 ? "increase" : "decrease";

        var userMessage =
            $"Field changed: {delta.FieldChanged}\n" +
            $"Previous value: {delta.PreviousValue}\n" +
            $"New value: {delta.NewValue}\n" +
            $"Lifetime total: ${delta.PreviousLifetimeTotal:N0} → ${delta.UpdatedLifetimeTotal:N0} ({direction} of ${Math.Abs(lifetimeDiff):N0})\n" +
            $"Current year: ${delta.PreviousCurrentYearTotal:N0} → ${delta.UpdatedCurrentYearTotal:N0}\n" +
            $"Present value: ${delta.PreviousPresentValue:N0} → ${delta.UpdatedPresentValue:N0}";

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, _narrativeSystemPrompt),
                new(ChatRole.User, userMessage)
            };

            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var text = response.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
                return text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Narrative generation failed for delta on {Field}", delta.FieldChanged);
        }

        // Fallback narrative
        return lifetimeDiff == 0
            ? $"Changing **{delta.FieldChanged}** from \"{delta.PreviousValue}\" to \"{delta.NewValue}\" has no impact on your projected costs."
            : $"Changing **{delta.FieldChanged}** from \"{delta.PreviousValue}\" to \"{delta.NewValue}\" would **{direction}** your lifetime Medicare costs by **${Math.Abs(lifetimeDiff):N0}**.";
    }
}
