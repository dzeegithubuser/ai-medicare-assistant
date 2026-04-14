using System.Text.Json.Serialization;

namespace Application.DTOs;

public class ProfileExtractRequest
{
    /// <summary>The raw user message from the chat input.</summary>
    public string Message { get; set; } = "";

    /// <summary>List of form field names that are still empty/missing.</summary>
    public List<string> MissingFields { get; set; } = [];
}

public class ProfileExtractResponse
{
    /// <summary>Profile fields extracted from the user message.</summary>
    [JsonPropertyName("extractedFields")]
    public Dictionary<string, object> ExtractedFields { get; set; } = new();

    /// <summary>Conversational reply confirming what was captured and asking for remaining fields.</summary>
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";
}
