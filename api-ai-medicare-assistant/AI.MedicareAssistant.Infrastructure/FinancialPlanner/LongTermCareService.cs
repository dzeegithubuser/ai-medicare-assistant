using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.FinancialPlanner;

public class LongTermCareService : ILongTermCareService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LongTermCareService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public LongTermCareService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LongTermCareService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<LongTermCareResponse> GetProjectionAsync(
        LongTermCareRequest request,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling LTC API for userEmail={UserEmail}, age={Age}, location={Location}",
            userEmail, request.Age, request.Location);

        var payload = new
        {
            userEmail,
            request.Age,
            request.PvAsOfYear,
            request.LifeExpectancy,
            request.TransactionTypeFlag,
            request.HealthProfile,
            location = request.Location,
            zipcode = request.Zipcode,
            request.Tobacco,
            request.CurrentLifeStyleExpenses,
            request.NumberOfAdultDayHealthCareLTCYears,
            request.NumberOfAssistedCareLTCYears,
            request.NumberOfHomeCareLTCYears,
            request.NumberOfNursingCareLTCYears,
            request.Gender,
            request.AlzheimersFlag,
            request.HeartStorkeFlag,
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/longTermCareR4");
        httpRequest.Content = JsonContent.Create(payload, options: JsonOptions);
        httpRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(_authToken);

        var requestBody = await httpRequest.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP longTermCareR4] Request: {Body}", requestBody);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP longTermCareR4] Response ({StatusCode}): {Body}",
            (int)response.StatusCode, responseBody);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<LongTermCareResponse>(responseBody, JsonOptions);
        if (result is null)
        {
            _logger.LogWarning("Empty response from LTC API for userEmail={UserEmail}", userEmail);
            return new LongTermCareResponse { WebServiceStatus = "EMPTY_RESPONSE" };
        }

        _logger.LogInformation(
            "LTC API returned projection for userEmail={UserEmail}, status={Status}",
            userEmail, result.WebServiceStatus);

        return result;
    }
}
