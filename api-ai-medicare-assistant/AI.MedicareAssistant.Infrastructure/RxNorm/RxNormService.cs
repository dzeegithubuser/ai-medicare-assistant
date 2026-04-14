using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RxNorm;

/// <summary>
/// Queries the NIH RxNorm REST API (rxnav.nlm.nih.gov) to normalize drug names
/// and retrieve drug-drug interactions.
/// </summary>
public class RxNormService : IRxNormService
{
    private readonly HttpClient _http;
    private readonly ILogger<RxNormService> _logger;
    private const string BaseUrl = "https://rxnav.nlm.nih.gov/REST";

    public RxNormService(HttpClient http, ILogger<RxNormService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<RxNormResult?> NormalizeDrug(string drugName)
    {
        if (string.IsNullOrWhiteSpace(drugName))
            return null;

        try
        {
            var encoded = Uri.EscapeDataString(drugName.Trim());
            var url = $"{BaseUrl}/rxcui.json?name={encoded}&search=1";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RxNorm API returned {Status} for drug '{Drug}'",
                    response.StatusCode, drugName);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<RxNormIdResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var rxCui = json?.IdGroup?.RxnormId?.FirstOrDefault();
            if (string.IsNullOrEmpty(rxCui))
            {
                // Try approximate match
                var approxUrl = $"{BaseUrl}/approximateTerm.json?term={encoded}&maxEntries=1";
                var approxResponse = await _http.GetAsync(approxUrl);
                if (approxResponse.IsSuccessStatusCode)
                {
                    var approxJson = await approxResponse.Content.ReadFromJsonAsync<ApproximateTermResponse>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var candidate = approxJson?.ApproximateGroup?.Candidate?.FirstOrDefault();
                    if (candidate != null && !string.IsNullOrEmpty(candidate.RxCUI))
                    {
                        return new RxNormResult { RxCui = candidate.RxCUI, NormalizedName = candidate.Name ?? drugName };
                    }
                }
                return null;
            }

            return new RxNormResult { RxCui = rxCui, NormalizedName = json?.IdGroup?.Name ?? drugName };
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("RxNorm API request timed out for drug '{Drug}'", drugName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to normalize drug '{Drug}' via RxNorm", drugName);
            return null;
        }
    }

    public async Task<List<DrugInteraction>> GetInteractions(List<string> rxCuis)
    {
        if (rxCuis.Count < 2)
            return [];

        try
        {
            var rxCuiList = string.Join("+", rxCuis);
            var url = $"{BaseUrl}/interaction/list.json?rxcuis={rxCuiList}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RxNorm interaction API returned {Status}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var interactions = new List<DrugInteraction>();

            if (doc.RootElement.TryGetProperty("fullInteractionTypeGroup", out var groups))
            {
                foreach (var group in groups.EnumerateArray())
                {
                    if (group.TryGetProperty("fullInteractionType", out var types))
                    {
                        foreach (var type in types.EnumerateArray())
                        {
                            if (type.TryGetProperty("interactionPair", out var pairs))
                            {
                                foreach (var pair in pairs.EnumerateArray())
                                {
                                    var severity = pair.TryGetProperty("severity", out var sev)
                                        ? sev.GetString() ?? "Unknown"
                                        : "Unknown";

                                    var description = pair.TryGetProperty("description", out var desc)
                                        ? desc.GetString() ?? ""
                                        : "";

                                    var drugNames = new List<string>();
                                    if (pair.TryGetProperty("interactionConcept", out var concepts))
                                    {
                                        foreach (var concept in concepts.EnumerateArray())
                                        {
                                            if (concept.TryGetProperty("minConceptItem", out var item) &&
                                                item.TryGetProperty("name", out var name))
                                            {
                                                drugNames.Add(name.GetString() ?? "");
                                            }
                                        }
                                    }

                                    interactions.Add(new DrugInteraction
                                    {
                                        DrugA = drugNames.ElementAtOrDefault(0) ?? "",
                                        DrugB = drugNames.ElementAtOrDefault(1) ?? "",
                                        Severity = MapSeverity(severity),
                                        Description = description,
                                        ClinicalConsequence = description,
                                        Recommendation = "Consult prescriber before combining these medications."
                                    });
                                }
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("RxNorm found {Count} interaction(s) for {CuiCount} drugs",
                interactions.Count, rxCuis.Count);

            return interactions;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("RxNorm interaction API request timed out");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch RxNorm interactions");
            return [];
        }
    }

    private static string MapSeverity(string rxNavSeverity) =>
        rxNavSeverity.ToUpperInvariant() switch
        {
            "HIGH" => "High",
            "N/A" => "Moderate",
            _ => "Moderate"
        };

    // --- Response DTOs ---

    private class RxNormIdResponse
    {
        public IdGroupData? IdGroup { get; set; }
    }

    private class IdGroupData
    {
        public string? Name { get; set; }
        public List<string>? RxnormId { get; set; }
    }

    private class ApproximateTermResponse
    {
        public ApproximateGroupData? ApproximateGroup { get; set; }
    }

    private class ApproximateGroupData
    {
        public List<CandidateData>? Candidate { get; set; }
    }

    private class CandidateData
    {
        public string? RxCUI { get; set; }
        public string? Name { get; set; }
    }

    public async Task<List<string>> GetNdcsByRxCui(string rxCui)
    {
        if (string.IsNullOrWhiteSpace(rxCui))
            return [];

        try
        {
            var url = $"{BaseUrl}/rxcui/{Uri.EscapeDataString(rxCui)}/ndcs.json";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RxNorm NDC API returned {Status} for RxCUI '{RxCui}'",
                    response.StatusCode, rxCui);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("ndcGroup", out var ndcGroup) &&
                ndcGroup.TryGetProperty("ndcList", out var ndcList) &&
                ndcList.TryGetProperty("ndc", out var ndcArray))
            {
                return ndcArray.EnumerateArray()
                    .Select(n => n.GetString() ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Take(10)
                    .ToList();
            }

            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch NDC codes for RxCUI '{RxCui}'", rxCui);
            return [];
        }
    }
}
