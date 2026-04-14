using System.Text.Json.Serialization;

namespace Application.DTOs;

public class PharmacySelectionExtractRequest
{
    /// <summary>The raw user message from chat.</summary>
    public string Message { get; set; } = "";

    /// <summary>Available pharmacies from the lookup results.</summary>
    public List<AvailablePharmacySummary> AvailablePharmacies { get; set; } = [];

    /// <summary>Currently selected pharmacies.</summary>
    public List<SelectedPharmacySummary> SelectedPharmacies { get; set; } = [];
}

public class AvailablePharmacySummary
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Zipcode { get; set; } = "";
}

public class SelectedPharmacySummary
{
    public string Name { get; set; } = "";
    public string PharmacyNumber { get; set; } = "";
}

public class PharmacySelectionExtractResponse
{
    [JsonPropertyName("pharmacyName")]
    public string? PharmacyName { get; set; }

    /// <summary>One or more exact pharmacy names from AVAILABLE_PHARMACIES for multi-select/remove.</summary>
    [JsonPropertyName("pharmacyNames")]
    public List<string>? PharmacyNames { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = "select";

    [JsonPropertyName("searchTerm")]
    public string? SearchTerm { get; set; }

    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";
}
