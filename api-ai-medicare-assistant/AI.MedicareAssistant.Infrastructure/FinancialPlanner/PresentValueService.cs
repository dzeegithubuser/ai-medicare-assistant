using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.FinancialPlanner;

public class PresentValueService : IPresentValueService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PresentValueService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PresentValueService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PresentValueService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<PresentValueResponse> CalculateAsync(
        PresentValueRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling expensesPresentValue for fromYear={From}, toYear={To}, discount={Discount}",
            request.FromYear, request.ToYear, request.Discount);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/expensesPresentValue");
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        if (!string.IsNullOrEmpty(_authToken))
            httpRequest.Headers.TryAddWithoutValidation("Authorization", _authToken);

        var requestBody = await httpRequest.Content!.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP expensesPresentValue] Request: {Body}", requestBody);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP expensesPresentValue] Response ({StatusCode}): {Body}",
            (int)response.StatusCode, responseBody);

        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<PresentValueResponse>(responseBody, JsonOptions);

        if (result is null)
        {
            _logger.LogWarning("expensesPresentValue returned null response");
            return new PresentValueResponse { WebServiceStatus = "EMPTY_RESPONSE" };
        }

        _logger.LogInformation(
            "expensesPresentValue completed: status={Status}, pvCount={Count}",
            result.WebServiceStatus, result.PvList.Count);

        return result;
    }
}
