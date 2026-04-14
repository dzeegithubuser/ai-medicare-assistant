using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.CountyLookup;

/// <summary>
/// ZIP-to-county lookup using the Financial Planner getCountycodeList API.
/// Results are cached in memory for 1 hour per ZIP code.
/// </summary>
public class CountyLookupService : ICountyLookupService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CountyLookupService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CountyLookupService(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<CountyLookupService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<string?> GetCountyCode(string zipCode)
    {
        var entries = await FetchCountyCodesAsync(zipCode);
        return entries.Count > 0 ? entries[0].CountyCode : null;
    }

    public async Task<string?> GetCountyName(string zipCode)
    {
        var entries = await FetchCountyCodesAsync(zipCode);
        return entries.Count > 0 ? entries[0].CountyName : null;
    }

    public async Task<string?> GetStateCode(string zipCode)
    {
        var entries = await FetchCountyCodesAsync(zipCode);
        return entries.Count > 0 ? entries[0].StateCode : null;
    }

    public async Task<List<CountyCodeEntry>> GetCountyCodeList(string zipCode)
    {
        var entries = await FetchCountyCodesAsync(zipCode);
        return entries.Select(e => new CountyCodeEntry
        {
            City = e.City ?? "",
            CountyName = e.CountyName ?? "",
            CountyCode = e.CountyCode ?? "",
            State = e.StateCode ?? "",
            Latitude = e.Latitude,
            Longitude = e.Longitude
        }).ToList();
    }

    private async Task<List<CountyCodeApiEntry>> FetchCountyCodesAsync(string zipCode)
    {
        var zip = zipCode?.Trim();
        if (string.IsNullOrEmpty(zip)) return [];

        if (zip.Length < 5)
            zip = zip.PadLeft(5, '0');

        var cacheKey = $"county_codes_{zip}";
        if (_cache.TryGetValue(cacheKey, out List<CountyCodeApiEntry>? cached) && cached is not null)
            return cached;

        try
        {
            _logger.LogInformation("Fetching county codes from Financial Planner API for ZIP {Zip}", zip);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/getCountycodeList");
            var payload = JsonSerializer.Serialize(new { zipcode = zip });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            request.Content = content;
            if (!string.IsNullOrEmpty(_authToken))
                request.Headers.TryAddWithoutValidation("Authorization", _authToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var wrapper = await response.Content.ReadFromJsonAsync<CountyCodeApiResponse>(JsonOptions);
            var entries = wrapper?.CountycodeList ?? [];

            _cache.Set(cacheKey, entries, CacheDuration);
            _logger.LogInformation("Cached {Count} county entries for ZIP {Zip}", entries.Count, zip);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch county codes for ZIP {Zip}", zip);
            return [];
        }
    }

    private class CountyCodeApiResponse
    {
        [JsonPropertyName("webServiceTransactionId")]
        public string? WebServiceTransactionId { get; set; }

        [JsonPropertyName("webServiceStatus")]
        public string? WebServiceStatus { get; set; }

        [JsonPropertyName("zipcode")]
        public string? Zipcode { get; set; }

        [JsonPropertyName("countycodeList")]
        public List<CountyCodeApiEntry> CountycodeList { get; set; } = [];
    }

    private class CountyCodeApiEntry
    {
        [JsonPropertyName("zipcode")]
        public string? Zipcode { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("stateCode")]
        public string? StateCode { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("countyCode")]
        public string? CountyCode { get; set; }

        [JsonPropertyName("countyName")]
        public string? CountyName { get; set; }
    }
}
 