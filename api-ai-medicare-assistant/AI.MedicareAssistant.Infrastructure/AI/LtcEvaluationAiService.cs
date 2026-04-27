using System.Text;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class LtcEvaluationAiService : ILtcEvaluationAiService
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<LtcEvaluationAiService> _logger;

    public LtcEvaluationAiService(
        IAiCompletionService aiService,
        PromptBuilder promptBuilder,
        ILogger<LtcEvaluationAiService> logger)
    {
        _aiService = aiService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<LtcCostEvaluation> EvaluateAsync(
        LongTermCareResponse projection,
        int age,
        string state,
        int lifeExpectancy,
        int healthProfile,
        string gender,
        CancellationToken cancellationToken = default)
    {
        var yearlyBreakdown = BuildYearlyBreakdown(projection);

        var healthProfileLabel = healthProfile switch
        {
            1 => "Excellent",
            2 => "Good",
            3 => "Average",
            4 => "Below Average",
            5 => "Poor",
            _ => healthProfile.ToString()
        };

        var (systemPrompt, userPrompt) = _promptBuilder.Build("ltc-evaluation", new Dictionary<string, string>
        {
            ["{{AGE}}"] = age.ToString(),
            ["{{GENDER}}"] = gender,
            ["{{STATE}}"] = state,
            ["{{HEALTH_PROFILE}}"] = healthProfileLabel,
            ["{{LIFE_EXPECTANCY}}"] = lifeExpectancy.ToString(),
            ["{{ADULT_DAY_YEARS}}"] = projection.NumberOfAdultDayHealthCareLTCYears.ToString(),
            ["{{ADULT_DAY_START_YEAR}}"] = projection.StartingYearOfAdultDayHealthCare.ToString(),
            ["{{HOME_CARE_YEARS}}"] = projection.NumberOfHomeCareLTCYears.ToString(),
            ["{{HOME_CARE_START_YEAR}}"] = projection.StartingYearOfHomeCare.ToString(),
            ["{{ASSISTED_CARE_YEARS}}"] = projection.NumberOfAssistedCareLTCYears.ToString(),
            ["{{ASSISTED_CARE_START_YEAR}}"] = projection.StartingYearOfAssistedCare.ToString(),
            ["{{NURSING_CARE_YEARS}}"] = projection.NumberOfNursingCareLTCYears.ToString(),
            ["{{NURSING_CARE_START_YEAR}}"] = projection.StartingYearOfNursingCare.ToString(),
            ["{{ADULT_DAY_TOTAL}}"] = projection.ExpectedAdultDayHealthCare.ToString("F2"),
            ["{{ADULT_DAY_PV}}"] = projection.PresentValueExpectedAdultDayHealthCare.ToString("F2"),
            ["{{HOME_CARE_TOTAL}}"] = projection.ExpectedHomeCare.ToString("F2"),
            ["{{HOME_CARE_PV}}"] = projection.PresentValueExpectedHomeCare.ToString("F2"),
            ["{{ASSISTED_CARE_TOTAL}}"] = projection.ExpectedAssistedCare.ToString("F2"),
            ["{{ASSISTED_CARE_PV}}"] = projection.PresentValueExpectedAssistedCare.ToString("F2"),
            ["{{NURSING_CARE_TOTAL}}"] = projection.ExpectedNursingCare.ToString("F2"),
            ["{{NURSING_CARE_PV}}"] = projection.PresentValueExpectedNursingCare.ToString("F2"),
            ["{{YEARLY_BREAKDOWN}}"] = yearlyBreakdown
        });

        _logger.LogInformation("Requesting AI LTC cost evaluation for {State}, age {Age}", state, age);

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
            return AiResponseParser.ParseJsonWithFallback(raw, new LtcCostEvaluation(), _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI LTC evaluation failed for {State}, age {Age}, returning empty evaluation", state, age);
            return new LtcCostEvaluation();
        }
    }

    private static string BuildYearlyBreakdown(LongTermCareResponse projection)
    {
        // Collect all unique years across all care types
        var yearData = new SortedDictionary<int, (decimal adultDay, decimal home, decimal assisted, decimal nursing)>();

        foreach (var e in projection.FutureAdultDayHealthCareExpenseList)
        {
            if (!yearData.ContainsKey(e.Year))
                yearData[e.Year] = (0, 0, 0, 0);
            var d = yearData[e.Year];
            yearData[e.Year] = (e.Expense, d.home, d.assisted, d.nursing);
        }

        foreach (var e in projection.FutureHomeCareExpenseList)
        {
            if (!yearData.ContainsKey(e.Year))
                yearData[e.Year] = (0, 0, 0, 0);
            var d = yearData[e.Year];
            yearData[e.Year] = (d.adultDay, e.Expense, d.assisted, d.nursing);
        }

        foreach (var e in projection.FutureAssistedCareExpensesList)
        {
            if (!yearData.ContainsKey(e.Year))
                yearData[e.Year] = (0, 0, 0, 0);
            var d = yearData[e.Year];
            yearData[e.Year] = (d.adultDay, d.home, e.Expense, d.nursing);
        }

        foreach (var e in projection.FutureNursingCareExpensesList)
        {
            if (!yearData.ContainsKey(e.Year))
                yearData[e.Year] = (0, 0, 0, 0);
            var d = yearData[e.Year];
            yearData[e.Year] = (d.adultDay, d.home, d.assisted, e.Expense);
        }

        var sb = new StringBuilder();
        foreach (var (year, costs) in yearData)
        {
            var total = costs.adultDay + costs.home + costs.assisted + costs.nursing;
            sb.AppendLine(
                $"Year {year}: AdultDay=${costs.adultDay:F2}, HomeCare=${costs.home:F2}, " +
                $"AssistedCare=${costs.assisted:F2}, NursingCare=${costs.nursing:F2}, Total=${total:F2}");
        }

        return sb.ToString().TrimEnd();
    }
}
