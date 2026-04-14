using System.Text.Json.Serialization;

namespace Domain.Models;

/// <summary>
/// Combined result of financial planner calculation + AI evaluation.
/// </summary>
public class CostProjectionResult
{
    [JsonPropertyName("yearlyDetails")]
    public List<IndividualMedicareDetail> YearlyDetails { get; set; } = [];

    [JsonPropertyName("lifetimeTotals")]
    public LifetimeTotals LifetimeTotals { get; set; } = new();

    [JsonPropertyName("evaluation")]
    public CostEvaluation Evaluation { get; set; } = new();

    [JsonPropertyName("presentValue")]
    public decimal PresentValue { get; set; }
}

public class LifetimeTotals
{
    [JsonPropertyName("lifeTimeABMedicareAdvantageExpenses")]
    public decimal LifeTimeABMedicareAdvantageExpenses { get; set; }

    [JsonPropertyName("lifeTimeABMedicareAdvantagePremium")]
    public decimal LifeTimeABMedicareAdvantagePremium { get; set; }

    [JsonPropertyName("lifeTimeABMedicareAdvantageOop")]
    public decimal LifeTimeABMedicareAdvantageOop { get; set; }

    [JsonPropertyName("lifeTimeDSurcharge")]
    public decimal LifeTimeDSurcharge { get; set; }

    [JsonPropertyName("lifeTimeBSurcharge")]
    public decimal LifeTimeBSurcharge { get; set; }

    [JsonPropertyName("totalIrmaa")]
    public decimal TotalIrmaa { get; set; }

    [JsonPropertyName("lifeTimeConciergePremium")]
    public decimal LifeTimeConciergePremium { get; set; }

    [JsonPropertyName("supplementPlanType")]
    public string SupplementPlanType { get; set; } = "";

    [JsonPropertyName("supplementPlanPremium")]
    public decimal SupplementPlanPremium { get; set; }

    [JsonPropertyName("conciergeIncluded")]
    public bool ConciergeIncluded { get; set; }

    [JsonPropertyName("lifeTimeABGDExpenses")]
    public decimal LifeTimeABGDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABGDPremium")]
    public decimal LifeTimeABGDPremium { get; set; }

    [JsonPropertyName("lifeTimeABGDOop")]
    public decimal LifeTimeABGDOop { get; set; }

    [JsonPropertyName("lifeTimeABFDExpenses")]
    public decimal LifeTimeABFDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABFDPremium")]
    public decimal LifeTimeABFDPremium { get; set; }

    [JsonPropertyName("lifeTimeABFDOop")]
    public decimal LifeTimeABFDOop { get; set; }

    [JsonPropertyName("lifeTimeABNDExpenses")]
    public decimal LifeTimeABNDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABNDPremium")]
    public decimal LifeTimeABNDPremium { get; set; }

    [JsonPropertyName("lifeTimeABNDOop")]
    public decimal LifeTimeABNDOop { get; set; }

    [JsonPropertyName("lifeTimeABCDExpenses")]
    public decimal LifeTimeABCDExpenses { get; set; }

    [JsonPropertyName("lifeTimeABCDPremium")]
    public decimal LifeTimeABCDPremium { get; set; }

    [JsonPropertyName("lifeTimeABCDOop")]
    public decimal LifeTimeABCDOop { get; set; }
}

public class CostEvaluation
{
    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = "";

    [JsonPropertyName("planBundleCode")]
    public string PlanBundleCode { get; set; } = "";

    [JsonPropertyName("lifetimeSummary")]
    public LifetimeSummary LifetimeSummary { get; set; } = new();

    [JsonPropertyName("costTrajectory")]
    public string CostTrajectory { get; set; } = "";

    [JsonPropertyName("trajectoryExplanation")]
    public string TrajectoryExplanation { get; set; } = "";

    [JsonPropertyName("yearlyHighlights")]
    public List<YearlyHighlight> YearlyHighlights { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<CostCategory> Categories { get; set; } = [];

    [JsonPropertyName("savingsTips")]
    public List<SavingsTip> SavingsTips { get; set; } = [];

    [JsonPropertyName("overallAssessment")]
    public string OverallAssessment { get; set; } = "";
}

public class LifetimeSummary
{
    [JsonPropertyName("totalPremiums")]
    public decimal TotalPremiums { get; set; }

    [JsonPropertyName("totalOutOfPocket")]
    public decimal TotalOutOfPocket { get; set; }

    [JsonPropertyName("totalCombined")]
    public decimal TotalCombined { get; set; }

    [JsonPropertyName("projectionYears")]
    public int ProjectionYears { get; set; }

    [JsonPropertyName("averageAnnualCost")]
    public decimal AverageAnnualCost { get; set; }
}

public class YearlyHighlight
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

public class CostCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("lifetimeTotal")]
    public decimal LifetimeTotal { get; set; }

    [JsonPropertyName("percentOfTotal")]
    public decimal PercentOfTotal { get; set; }

    [JsonPropertyName("trend")]
    public string Trend { get; set; } = "";

    [JsonPropertyName("insight")]
    public string Insight { get; set; } = "";
}

public class SavingsTip
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
