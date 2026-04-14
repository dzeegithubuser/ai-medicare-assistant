using System.Text.Json.Serialization;

namespace Domain.Models;

/// <summary>
/// Combined result of LTC financial planner projection + AI evaluation.
/// </summary>
public class LtcProjectionResult
{
    [JsonPropertyName("projection")]
    public LongTermCareResponse Projection { get; set; } = new();

    [JsonPropertyName("evaluation")]
    public LtcCostEvaluation Evaluation { get; set; } = new();
}

public class LtcCostEvaluation
{
    [JsonPropertyName("lifetimeSummary")]
    public LtcLifetimeSummary LifetimeSummary { get; set; } = new();

    [JsonPropertyName("costTrajectory")]
    public string CostTrajectory { get; set; } = "";

    [JsonPropertyName("trajectoryExplanation")]
    public string TrajectoryExplanation { get; set; } = "";

    [JsonPropertyName("yearlyHighlights")]
    public List<LtcYearlyHighlight> YearlyHighlights { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<LtcCostCategory> Categories { get; set; } = [];

    [JsonPropertyName("savingsTips")]
    public List<LtcSavingsTip> SavingsTips { get; set; } = [];

    [JsonPropertyName("overallAssessment")]
    public string OverallAssessment { get; set; } = "";
}

public class LtcLifetimeSummary
{
    [JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("totalPresentValue")]
    public decimal TotalPresentValue { get; set; }

    [JsonPropertyName("projectionYears")]
    public int ProjectionYears { get; set; }

    [JsonPropertyName("averageAnnualCost")]
    public decimal AverageAnnualCost { get; set; }
}

public class LtcYearlyHighlight
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("flag")]
    public string Flag { get; set; } = "";

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = "";
}

public class LtcCostCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("lifetimeTotal")]
    public decimal LifetimeTotal { get; set; }

    [JsonPropertyName("presentValue")]
    public decimal PresentValue { get; set; }

    [JsonPropertyName("percentOfTotal")]
    public decimal PercentOfTotal { get; set; }

    [JsonPropertyName("trend")]
    public string Trend { get; set; } = "";

    [JsonPropertyName("insight")]
    public string Insight { get; set; } = "";
}

public class LtcSavingsTip
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("estimatedSavings")]
    public string EstimatedSavings { get; set; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "";
}
