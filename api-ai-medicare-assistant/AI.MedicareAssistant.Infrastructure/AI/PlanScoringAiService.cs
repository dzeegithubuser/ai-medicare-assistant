using System.Text.Json;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class PlanScoringAiService : IPlanScoringAiService
{
    private readonly IAiCompletionService _aiService;
    private readonly IMemoryCache _cache;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<PlanScoringAiService> _logger;

    public PlanScoringAiService(
        IAiCompletionService aiService,
        IMemoryCache cache,
        PromptBuilder promptBuilder,
        ILogger<PlanScoringAiService> logger)
    {
        _aiService = aiService;
        _cache = cache;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<PlanRecommendationResult> ScorePlansAsync(
        PlanRecommendationRequest request,
        string countyName,
        CancellationToken cancellationToken = default)
    {
        // Build cache key from stable inputs (include pharmacy NPIs so different selections aren't cached together)
        var rxKey = string.Join(",", request.RxCuis.OrderBy(x => x));
        var phKey = request.SelectedPharmacies is { Count: > 0 }
            ? string.Join(",", request.SelectedPharmacies.Select(p => p.Npi).OrderBy(x => x))
            : "none";
        var cacheKey = $"plan-rec:{request.ZipCode}:{rxKey}:{request.MagiTier}:{request.AnnualIncome}:{phKey}";

        if (_cache.TryGetValue(cacheKey, out PlanRecommendationResult? cached) && cached is not null)
        {
            _logger.LogInformation("Returning cached plan recommendation for ZIP {Zip}", request.ZipCode);
            return cached;
        }

        // Build drug list text
        var drugLines = request.DrugSummaries.Select(d =>
        {
            var genericNote = !string.Equals(d.DrugName, d.GenericName, StringComparison.OrdinalIgnoreCase)
                ? $" (generic: {d.GenericName})"
                : " (generic)";
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(d.SelectedName)) details.Add($"selected: {d.SelectedName} ({d.NameType})");
            if (!string.IsNullOrWhiteSpace(d.TherapeuticCategory)) details.Add($"category: {d.TherapeuticCategory}");
            if (!string.IsNullOrWhiteSpace(d.DrugClass)) details.Add($"class: {d.DrugClass}");
            if (!string.IsNullOrWhiteSpace(d.DosageForm)) details.Add($"{d.DosageForm}");
            if (!string.IsNullOrWhiteSpace(d.Strength)) details.Add($"{d.Strength}");
            if (!string.IsNullOrWhiteSpace(d.Packaging)) details.Add($"{d.Packaging}");
            var detailStr = details.Count > 0 ? $" [{string.Join(", ", details)}]" : "";
            return $"- {d.DrugName}{genericNote}, RxCUI: {d.RxCui}{detailStr}";
        });

        var (systemPrompt, userPrompt) = _promptBuilder.Build("plan-scoring", new Dictionary<string, string>
        {
            ["{{AGE}}"] = (request.Age ?? 65).ToString(),
            ["{{ZIP_CODE}}"] = request.ZipCode,
            ["{{COUNTY_NAME}}"] = countyName,
            ["{{MAGI_TIER}}"] = request.MagiTier,
            ["{{ANNUAL_INCOME}}"] = request.AnnualIncome.ToString("F0"),
            ["{{HOUSEHOLD_SIZE}}"] = request.HouseholdSize.ToString(),
            ["{{FILING_STATUS}}"] = request.IncomeFilingStatus,
            ["{{HAS_EMPLOYER_COVERAGE}}"] = request.HasEmployerCoverage ? "Yes" : "No",
            ["{{DISABILITY_STATUS}}"] = request.DisabilityStatus ?? "None",
            ["{{HAS_CHRONIC_CONDITION}}"] = request.HasChronicCondition ? "Yes" : "No",
            ["{{CHRONIC_DETAILS}}"] = request.ChronicConditionDetails ?? "None",
            ["{{LIS_TIER}}"] = DetermineLisTierString(request),
            ["{{DRUG_LIST}}"] = string.Join("\n", drugLines),
            ["{{PHARMACY_CONTEXT}}"] = BuildPharmacyContext(request)
        });

        _logger.LogInformation(
            "Requesting plan recommendations for ZIP {Zip}, {DrugCount} drugs, income ${Income}",
            request.ZipCode, request.DrugSummaries.Count, request.AnnualIncome);

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, userPrompt, cancellationToken);

            _logger.LogDebug("Plan scoring AI response: {Length} chars", raw.Length);

            var result = AiResponseParser.ParseJson<PlanRecommendationResult>(raw, _logger);

            if (result is null || result.RankedPlans.Count == 0)
            {
                _logger.LogWarning("AI returned empty plan recommendations, using fallback");
                return BuildFallback(request);
            }

            // Cache for 24 hours
            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plan scoring AI call failed, returning fallback");
            return BuildFallback(request);
        }
    }

    private static string DetermineLisTierString(PlanRecommendationRequest req)
    {
        var income = req.AnnualIncome;
        var size = req.HouseholdSize;

        // 2025 FPL thresholds with household scaling
        var fullLimit = 22590m + Math.Max(0, size - 1) * 8070m;
        var partialLimit = 33240m + Math.Max(0, size - 1) * 11640m;

        if (income <= fullLimit) return "Full";
        if (income <= partialLimit) return "Partial";
        return "None";
    }

    private static string BuildPharmacyContext(PlanRecommendationRequest request)
    {
        if (request.SelectedPharmacies is not { Count: > 0 })
            return "No pharmacies selected — use general copay estimates.";

        var lines = request.SelectedPharmacies.Select((p, i) =>
            $"{i + 1}. {p.Name} (NPI: {p.Npi}, Type: {p.PharmacyType})");
        return string.Join("\n", lines)
            + "\nFactor each pharmacy's type (chain vs independent, retail vs specialty) into copay estimates. Preferred network pharmacies typically have lower copays.";
    }

    private static PlanRecommendationResult BuildFallback(PlanRecommendationRequest request)
    {
        return new PlanRecommendationResult
        {
            LisEligible = false,
            LisTier = "None",
            RecommendedPlanType = "MedicareAdvantage",
            EligibilitySummary = "We were unable to generate personalized plan recommendations at this time. " +
                                 "Please visit Medicare.gov Plan Finder for comprehensive plan comparison.",
            RankedPlans = []
        };
    }
}
