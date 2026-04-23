using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.FinancialPlanner;

public class FinancialPlannerDrugService : IFinancialPlannerDrugService
{
    private readonly HttpClient _httpClient;
    private readonly IChatClient _chatClient;
    private readonly ILogger<FinancialPlannerDrugService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FinancialPlannerDrugService(
        HttpClient httpClient,
        IChatClient chatClient,
        IConfiguration configuration,
        ILogger<FinancialPlannerDrugService> logger)
    {
        _httpClient = httpClient;
        _chatClient = chatClient;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    private async Task<DrugSearchResponse> SearchDrugAsync(string drugName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching drug via Financial Planner API: {DrugName}", drugName);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/drugSearch");
        request.Content = JsonContent.Create(new DrugSearchRequest { DrugName = drugName }, options: JsonOptions);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(_authToken);

        var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP drugSearch] Request: {Body}", requestBody);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP drugSearch] Response ({StatusCode}): {Body}",
            (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "drugSearch failed with status {StatusCode} for drug={DrugName}. Response: {Body}",
                (int)response.StatusCode, drugName, responseBody);
            response.EnsureSuccessStatusCode();
        }

        var result = JsonSerializer.Deserialize<DrugSearchResponse>(responseBody, JsonOptions);
        if (result is null)
        {
            _logger.LogWarning("Empty response from drugSearch for {DrugName}", drugName);
            return new DrugSearchResponse { WebServiceStatus = "EMPTY_RESPONSE" };
        }

        _logger.LogInformation("Drug search returned {Count} results for {DrugName}", result.DrugList.Count, drugName);
        return result;
    }

    private async Task<DrugDetailResponse> GetDrugDetailAsync(string rxcui, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting drug detail via Financial Planner API for rxcui={Rxcui}", rxcui);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/getDrugDetailAdvance");
        request.Content = JsonContent.Create(new DrugDetailRequest { Rxcui = rxcui }, options: JsonOptions);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(_authToken);

        var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP getDrugDetailAdvance] Request: {Body}", requestBody);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP getDrugDetailAdvance] Response ({StatusCode}): {Body}",
            (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "getDrugDetailAdvance failed with status {StatusCode} for rxcui={Rxcui}. Response: {Body}",
                (int)response.StatusCode, rxcui, responseBody);
            response.EnsureSuccessStatusCode();
        }

        var result = JsonSerializer.Deserialize<DrugDetailResponse>(responseBody, JsonOptions);
        if (result is null)
        {
            _logger.LogWarning("Empty response from getDrugDetailAdvance for rxcui={Rxcui}", rxcui);
            return new DrugDetailResponse { WebServiceStatus = "EMPTY_RESPONSE" };
        }

        foreach (var item in result.DrugDetailAdvanceList)
        {
            item.DrugType = string.IsNullOrWhiteSpace(item.BrandName) ? "Generic" : "Branded";
        }

        _logger.LogInformation("Drug detail retrieved for rxcui={Rxcui}", rxcui);
        return result;
    }

    private async Task<DrugSearchResult> SearchAndMatchAsync(string drugName, CancellationToken cancellationToken = default)
    {
        var searchResponse = await SearchDrugAsync(drugName, cancellationToken);

        if (searchResponse.WebServiceStatus != "SUCCESS")
            return new DrugSearchResult { DrugName = drugName, Search = searchResponse };

        var matched = searchResponse.DrugList
            .FirstOrDefault(d => d.DisplayName.Trim().Equals(drugName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (matched is null)
            return new DrugSearchResult { DrugName = drugName, Search = searchResponse };

        var detail = await GetDrugDetailAsync(matched.Rxcui, cancellationToken);

        return new DrugSearchResult
        {
            DrugName = drugName,
            Search = searchResponse,
            MatchedDrug = matched,
            Detail = detail
        };
    }

public async Task<BulkDrugSearchResponse> SearchBulkAsync(List<string> drugNames, CancellationToken cancellationToken = default)
    {
        var results = new List<DrugSearchResult>();

        foreach (var drugName in drugNames)
        {
            var result = await SearchAndMatchAsync(drugName, cancellationToken);
            results.Add(result);
        }

        var response = new BulkDrugSearchResponse { Results = results };

        if (drugNames.Count > 1)
        {
            var analysis = await EvaluateInteractionsAsync(drugNames, cancellationToken);
            response.Interactions = analysis.Interactions;
            response.DuplicateTherapies = analysis.DuplicateTherapies;
        }

        return response;
    }

    private async Task<DrugInteractionAnalysis> EvaluateInteractionsAsync(
        List<string> drugNames, CancellationToken cancellationToken)
    {
        var drugList = string.Join("\n", drugNames.Select(d => $"- {d}"));

        var systemPrompt = """
            You are a clinical pharmacology assistant. Analyze drug combinations for safety.
            Return ONLY a JSON object with no additional text.
            """;

        var userPrompt = "Analyze the following drugs for interactions and duplicate therapies:\n\n"
            + drugList + "\n\n"
            + """
            Return a JSON object with this exact structure:
            {
              "interactions": [
                {
                  "drugA": "Drug A name",
                  "drugB": "Drug B name",
                  "severity": "High|Moderate|Low",
                  "description": "Brief description of the interaction",
                  "clinicalConsequence": "What could happen clinically",
                  "recommendation": "Clinical recommendation"
                }
              ],
              "duplicateTherapies": [
                {
                  "drugs": ["Drug1", "Drug2"],
                  "therapeuticClass": "The shared therapeutic class",
                  "message": "Brief explanation of the overlap"
                }
              ]
            }

            Rules:
            - Evaluate ALL pairwise interactions between the drugs.
            - Detect duplicate therapies where drugs share overlapping therapeutic classes.
            - If no interactions or duplicates exist, return empty arrays.
            - Do not invent interactions that are not clinically recognized.
            """;

        _logger.LogInformation("Evaluating drug interactions via AI for {Count} drugs", drugNames.Count);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        try
        {
            var aiResponse = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var text = aiResponse.Text.Trim();

            // Strip markdown code fences if present
            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    text = text[(firstNewline + 1)..lastFence].Trim();
            }

            var analysis = JsonSerializer.Deserialize<DrugInteractionAnalysis>(text, JsonOptions);
            _logger.LogInformation(
                "AI interaction analysis complete: {Interactions} interactions, {Duplicates} duplicate therapies",
                analysis?.Interactions.Count ?? 0, analysis?.DuplicateTherapies.Count ?? 0);

            return analysis ?? new DrugInteractionAnalysis();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate drug interactions via AI");
            return new DrugInteractionAnalysis();
        }
    }
}
