using System.Text.Json.Serialization;

namespace Application.DTOs;

public class DrugSelectionExtractRequest
{
    /// <summary>The raw user message from chat.</summary>
    public string Message { get; set; } = "";

    /// <summary>Available drugs with their formulation options (JSON-serialized summary).</summary>
    public List<AvailableDrugSummary> AvailableDrugs { get; set; } = [];
}

public class AvailableDrugSummary
{
    public string Name { get; set; } = "";
    public List<string> Types { get; set; } = [];
    public Dictionary<string, List<string>> DosageForms { get; set; } = new();
    public Dictionary<string, List<string>> Strengths { get; set; } = new();
}

public class DrugSelectionExtractResponse
{
    [JsonPropertyName("drugName")]
    public string? DrugName { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("dosageForm")]
    public string? DosageForm { get; set; }

    [JsonPropertyName("strength")]
    public string? Strength { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = "select";

    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";
}
