using Domain.Interfaces;
using Infrastructure.FinancialPlanner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Pharmacy;

/// <summary>
/// Pharmacy lookup using the Financial Planner getPharmacies API.
/// Returns pharmacies near a lat/lng with server-side pagination.
/// </summary>
public class FinancialPlannerPharmacyService : IPharmacyLookupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FinancialPlannerPharmacyService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public FinancialPlannerPharmacyService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FinancialPlannerPharmacyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<PharmacyLookupResponse> GetPharmaciesAsync(
        PharmacyLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching pharmacies from Financial Planner API: lat={Lat}, lng={Lng}, radius={Radius}, name={Name}, page={Page}, size={Size}",
            request.Latitude, request.Longitude, request.SearchRadiusInMiles,
            request.PharmacyName, request.Page, request.Size);

        try
        {
            var payload = new
            {
                latitude = request.Latitude,
                longitude = request.Longitude,
                searchRadiusInMiles = request.SearchRadiusInMiles,
                pharmacyName = request.PharmacyName,
                page = request.Page,
                size = request.Size
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/getPharmacies");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            if (!string.IsNullOrEmpty(_authToken))
                httpRequest.Headers.TryAddWithoutValidation("Authorization", _authToken);

            var curl = await httpRequest.ToCurlAsync();
            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PharmacyLookupResponse>(JsonOptions, cancellationToken);

            if (result is null)
            {
                _logger.LogWarning("getPharmacies returned null response");
                return new PharmacyLookupResponse { WebServiceStatus = "EMPTY_RESPONSE" };
            }

            _logger.LogInformation(
                "getPharmacies returned {Count} pharmacies (page {Page}/{TotalPages}, total {Total})",
                result.Pharmacies.Count, result.Page, result.TotalPages, result.TotalPharmacies);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch pharmacies from Financial Planner API");
            return new PharmacyLookupResponse { WebServiceStatus = "ERROR" };
        }
    }
}
