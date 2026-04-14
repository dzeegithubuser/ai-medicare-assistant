using System.Text.Json.Serialization;

namespace Application.DTOs;

public class PlanSelectionExtractRequest
{
    /// <summary>The raw user message from chat.</summary>
    public string Message { get; set; } = "";

    /// <summary>Currently active section: "partd" or "ma".</summary>
    public string? ActiveSection { get; set; }

    /// <summary>Available Part D plans (summary).</summary>
    public List<AvailablePlanSummary> AvailablePartDPlans { get; set; } = [];

    /// <summary>Available Medigap plans (summary).</summary>
    public List<AvailableMedigapSummary> AvailableMedigapPlans { get; set; } = [];

    /// <summary>Available Medicare Advantage plans (summary).</summary>
    public List<AvailablePlanSummary> AvailableMAPlans { get; set; } = [];

    /// <summary>Currently selected plans.</summary>
    public SelectedPlansSummary SelectedPlans { get; set; } = new();
}

public class AvailablePlanSummary
{
    public string PlanName { get; set; } = "";
    public string ContractId { get; set; } = "";
    public string PlanId { get; set; } = "";
    public decimal Premium { get; set; }
    public double StarRating { get; set; }
}

public class AvailableMedigapSummary
{
    public string Company { get; set; } = "";
    public string Plan { get; set; } = "";
    public decimal MonthlyPremium { get; set; }
}

public class SelectedPlansSummary
{
    public string? PartD { get; set; }
    public string? Medigap { get; set; }
    public string? Ma { get; set; }
    public string? MaGapPartD { get; set; }
}

public class PlanSelectionExtractResponse
{
    [JsonPropertyName("planName")]
    public string? PlanName { get; set; }

    [JsonPropertyName("planCategory")]
    public string? PlanCategory { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = "select";

    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";
}
