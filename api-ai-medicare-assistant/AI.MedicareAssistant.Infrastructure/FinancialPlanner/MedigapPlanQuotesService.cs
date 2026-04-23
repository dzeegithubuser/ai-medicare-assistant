using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.FinancialPlanner;

public class MedigapPlanQuotesService : IMedigapPlanQuotesService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MedigapPlanQuotesService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MedigapPlanQuotesService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MedigapPlanQuotesService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<MedigapPlanQuotesResponse> GetQuotesAsync(
        MedigapPlanQuotesRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling Medigap Plan Quotes API for zip={Zip5}, plan={Plan}, coverageYear={CoverageYear}",
            request.Zip5, request.Plan, request.CoverageYear);

        var queryString = string.Join("&",
            $"zip5={Uri.EscapeDataString(request.Zip5)}",
            $"gender={Uri.EscapeDataString(request.Gender)}",
            $"tobacco={request.Tobacco}",
            $"birthDate={Uri.EscapeDataString(request.BirthDate)}",
            $"plan={Uri.EscapeDataString(request.Plan)}",
            $"county={Uri.EscapeDataString(request.County)}",
            $"taxFilingStatus={Uri.EscapeDataString(request.TaxFilingStatus)}",
            $"magiTier={request.MagiTier}",
            $"healthProfile={request.HealthProfile}",
            $"coverageYear={Uri.EscapeDataString(request.CoverageYear)}",
            $"versionId={request.VersionId ?? "null"}");

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/medigapPlanQuotes?{queryString}");
        httpRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(_authToken);

        _logger.LogDebug("[FP medigapPlanQuotes] Request URL: {Url}", httpRequest.RequestUri);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP medigapPlanQuotes] Response ({StatusCode}): {Body}",
            (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "medigapPlanQuotes failed with status {StatusCode} for zip={Zip5}, plan={Plan}. Response: {Body}",
                (int)response.StatusCode, request.Zip5, request.Plan, responseBody);
            response.EnsureSuccessStatusCode();
        }

        var result = JsonSerializer.Deserialize<MedigapPlanQuotesResponse>(responseBody, JsonOptions);
        if (result is null)
        {
            _logger.LogWarning("Empty response from medigapPlanQuotes for zip={Zip5}", request.Zip5);
            return new MedigapPlanQuotesResponse();
        }

        _logger.LogInformation(
            "Medigap Plan Quotes returned {Count} plans for zip={Zip5}, plan={Plan}",
            result.PlanList.Count, request.Zip5, request.Plan);

        return result;
    }
}
