using System.Text.Json;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class CostEvaluationAiService : ICostEvaluationAiService
{
    private readonly IChatClient _chatClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<CostEvaluationAiService> _logger;

    public CostEvaluationAiService(
        IChatClient chatClient,
        PromptBuilder promptBuilder,
        ILogger<CostEvaluationAiService> logger)
    {
        _chatClient = chatClient;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<CostEvaluation> EvaluateAsync(
        IndividualMedicareResponse calcResult,
        string planName,
        string planBundleCode,
        int coverageYear,
        int lifeExpectancy,
        string taxFilingStatus,
        string stateName,
        string supplementPlanType,
        decimal supplementPlanPremium,
        CancellationToken cancellationToken = default)
    {
        var yearlyLines = calcResult.IndividualMedicares.Select(y =>
            $"Year {y.Year}: PartA Premium=${y.PartAPremium:F2}, PartB Premium=${y.PartBPremium:F2}, " +
            $"PartB Surcharge=${y.PartBPremiumSurcharge:F2}, MA Premium=${y.MedicareAdvantagePremium:F2}, " +
            $"PartD Premium=${y.PartDPremium:F2}, PartD Surcharge=${y.PartDPremiumSurcharge:F2}, " +
            $"Concierge=${y.ConciergePremium:F2}, PartA OOP=${y.PartAOOP:F2}, PartB OOP=${y.PartBOOP:F2}, " +
            $"PartD OOP=${y.PartDOOP:F2}, Total AB/MA=${y.TotalABMedicareAdvantage:F2}, " +
            $"Dental Premium={y.DentalPremium:F2}, Dental OOP={y.DentalOOP:F2}");

        var (systemPrompt, userPrompt) = _promptBuilder.BuildCostEvaluation(new Dictionary<string, string>
        {
            ["{{PLAN_NAME}}"] = planName,
            ["{{PLAN_BUNDLE_CODE}}"] = planBundleCode,
            ["{{COVERAGE_YEAR}}"] = coverageYear.ToString(),
            ["{{LIFE_EXPECTANCY}}"] = lifeExpectancy.ToString(),
            ["{{TAX_FILING_STATUS}}"] = taxFilingStatus,
            ["{{STATE_NAME}}"] = stateName,
            ["{{LIFETIME_AB_MA_EXPENSES}}"] = calcResult.LifeTimeABMedicareAdvantageExpenses.ToString("F2"),
            ["{{LIFETIME_AB_MA_PREMIUM}}"] = calcResult.LifeTimeABMedicareAdvantagePremium.ToString("F2"),
            ["{{LIFETIME_AB_MA_OOP}}"] = calcResult.LifeTimeABMedicareAdvantageOop.ToString("F2"),
            ["{{LIFETIME_D_SURCHARGE}}"] = calcResult.LifeTimeDSurcharge.ToString("F2"),
            ["{{LIFETIME_B_SURCHARGE}}"] = calcResult.LifeTimeBSurcharge.ToString("F2"),
            ["{{TOTAL_IRMAA}}"] = (calcResult.LifeTimeBSurcharge + calcResult.LifeTimeDSurcharge).ToString("F2"),
            ["{{SUPPLEMENT_PLAN_TYPE}}"] = supplementPlanType,
            ["{{SUPPLEMENT_PLAN_PREMIUM}}"] = supplementPlanPremium.ToString("F2"),
            ["{{YEARLY_BREAKDOWN}}"] = string.Join("\n", yearlyLines)
        });

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        _logger.LogInformation("Requesting AI cost evaluation for plan {PlanName}", planName);

        try
        {
            var aiResponse = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var raw = aiResponse.Text?.Trim() ?? "{}";

            // Strip markdown code fences if present
            if (raw.StartsWith("```"))
            {
                var firstNewline = raw.IndexOf('\n');
                var lastFence = raw.LastIndexOf("```");
                if (firstNewline >= 0 && lastFence > firstNewline)
                    raw = raw[(firstNewline + 1)..lastFence].Trim();
            }

            var result = JsonSerializer.Deserialize<CostEvaluation>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new CostEvaluation();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI cost evaluation failed for plan {PlanName}, returning empty evaluation", planName);
            return new CostEvaluation();
        }
    }
}
