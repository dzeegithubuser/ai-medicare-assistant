using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.FinancialPlanner;

public class PartDPlanRecommendationService : IPartDPlanRecommendationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PartDPlanRecommendationService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public PartDPlanRecommendationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PartDPlanRecommendationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<PartDPlanRecommendationResponse> RecommendAsync(
        PartDPlanRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling Part D Plan Recommendation API for user={UserId}, coverageYear={CoverageYear}, prescriptions={Count}",
            request.UserId, request.CoverageYear, request.Prescriptions.Count);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/partDPlanRecommendation");
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        httpRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(_authToken);

        var requestBody = await httpRequest.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP partDPlanRecommendation] Request: {Body}", requestBody);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP partDPlanRecommendation] Response ({StatusCode}): {Body}",
            (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "partDPlanRecommendation failed with status {StatusCode} for user={UserId}. Response: {Body}",
                (int)response.StatusCode, request.UserId, responseBody);
            response.EnsureSuccessStatusCode();
        }

        var result = JsonSerializer.Deserialize<PartDPlanRecommendationResponse>(responseBody, JsonOptions);
        if (result is null)
        {
            _logger.LogWarning("Empty response from partDPlanRecommendation for user={UserId}", request.UserId);
            return new PartDPlanRecommendationResponse { WebServiceStatus = "EMPTY_RESPONSE" };
        }

        _logger.LogInformation(
            "Part D Plan Recommendation returned {Count} plans for user={UserId}",
            result.RecommendationList.Count, request.UserId);

        return result;
    }
}
