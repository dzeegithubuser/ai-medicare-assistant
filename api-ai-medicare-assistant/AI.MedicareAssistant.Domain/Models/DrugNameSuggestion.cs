using System.Text.Json.Serialization;

namespace Domain.Models;

public class DrugNameSuggestionResult
{
    [JsonPropertyName("suggestions")]
    public List<DrugNameSuggestion> Suggestions { get; set; } = [];
}

public class DrugNameSuggestion
{
    [JsonPropertyName("inputName")]
    public string InputName { get; set; } = "";

    [JsonPropertyName("candidates")]
    public List<DrugCandidate> Candidates { get; set; } = [];
}

public class DrugCandidate
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
