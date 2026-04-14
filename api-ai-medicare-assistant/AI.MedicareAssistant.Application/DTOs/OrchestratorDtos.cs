using System.Text.Json.Serialization;

namespace Application.DTOs;

public class OrchestratorRequest
{
    public string Message { get; set; } = "";
    /// <summary>Relative URL of the Angular page where the user sent the message (e.g. /analysis/fp-drugs).</summary>
    public string? CurrentPage { get; set; }
}

public class OrchestratorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("requiresConfirmation")]
    public bool RequiresConfirmation { get; set; }

    [JsonPropertyName("delta")]
    public DeltaResult? Delta { get; set; }

    [JsonPropertyName("displayData")]
    public DisplayData? DisplayData { get; set; }

    [JsonPropertyName("nextIntent")]
    public string? NextIntent { get; set; }
}

public class DeltaResult
{
    [JsonPropertyName("previousLifetimeTotal")]
    public decimal PreviousLifetimeTotal { get; set; }

    [JsonPropertyName("updatedLifetimeTotal")]
    public decimal UpdatedLifetimeTotal { get; set; }

    [JsonPropertyName("previousCurrentYearTotal")]
    public decimal PreviousCurrentYearTotal { get; set; }

    [JsonPropertyName("updatedCurrentYearTotal")]
    public decimal UpdatedCurrentYearTotal { get; set; }

    [JsonPropertyName("previousPresentValue")]
    public decimal PreviousPresentValue { get; set; }

    [JsonPropertyName("updatedPresentValue")]
    public decimal UpdatedPresentValue { get; set; }

    [JsonPropertyName("fieldChanged")]
    public string FieldChanged { get; set; } = "";

    [JsonPropertyName("previousValue")]
    public string PreviousValue { get; set; } = "";

    [JsonPropertyName("newValue")]
    public string NewValue { get; set; } = "";

    [JsonPropertyName("narrativeSummary")]
    public string NarrativeSummary { get; set; } = "";

    [JsonPropertyName("ltcPresentValueDelta")]
    public decimal? LtcPresentValueDelta { get; set; }
}

public class DisplayData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}

/// <summary>
/// Response from the orchestrator intent classifier (19 domain intents).
/// </summary>
public class OrchestratorIntentResult
{
    public string Intent { get; set; } = "unknown";
    public Dictionary<string, string?> Params { get; set; } = [];
}
