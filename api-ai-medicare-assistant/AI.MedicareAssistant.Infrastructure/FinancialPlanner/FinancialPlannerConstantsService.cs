using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.FinancialPlanner;

public class FinancialPlannerConstantsService : IConstantsService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FinancialPlannerConstantsService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;
    private const string CacheKey = "fp_constants";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FinancialPlannerConstantsService(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<FinancialPlannerConstantsService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<List<ConstantItem>> GetAllAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<ConstantItem>? cached) && cached is not null)
            return cached;

        try
        {
            _logger.LogInformation("Fetching constants from Financial Planner API");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/getConstants");
            var content = new StringContent(string.Empty);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
            if (!string.IsNullOrEmpty(_authToken))
                request.Headers.TryAddWithoutValidation("Authorization", _authToken);

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("[FP getConstants] Response ({StatusCode}): {Body}",
                (int)response.StatusCode, responseBody);
            response.EnsureSuccessStatusCode();

            var apiItems = JsonSerializer.Deserialize<List<ApiConstant>>(responseBody, JsonOptions);
            var items = apiItems?.Select(MapToConstantItem).ToList() ?? [];

            _cache.Set(CacheKey, items, CacheDuration);
            _logger.LogInformation("Cached {Count} constants from Financial Planner API", items.Count);

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch constants from Financial Planner API");
            return [];
        }
    }

    public async Task<ConstantItem?> GetByLabelAsync(string label)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(c =>
            c.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<ConstantItem>> GetByFilterAsync(Func<ConstantItem, bool> predicate)
    {
        var all = await GetAllAsync();
        return all.Where(predicate).ToList();
    }

    public async Task<string?> GetValueByLabelAsync(string label)
    {
        var item = await GetByLabelAsync(label);
        return item?.Value;
    }

    public async Task<List<string>> GetValueListByLabelAsync(string label)
    {
        var item = await GetByLabelAsync(label);
        return item?.GetValueList() ?? [];
    }

    public async Task<List<LabelValuePair>> GetValuePairsByLabelAsync(string label)
    {
        var item = await GetByLabelAsync(label);
        return item?.GetValuePairs() ?? [];
    }

    private static ConstantItem MapToConstantItem(ApiConstant api) => new()
    {
        Id = api.Id ?? "",
        Label = api.Lable ?? "",  // API uses "lable" (typo in external API)
        Value = api.Value ?? "",
        Description = api.Description ?? "",
        Status = api.Status ?? "",
        Year = api.Year
    };

    /// <summary>
    /// Maps the external API's JSON shape (note: "lable" typo is in the API).
    /// </summary>
    private class ApiConstant
    {
        public string? Id { get; set; }
        [JsonPropertyName("lable")]
        public string? Lable { get; set; }
        public string? Value { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public int Year { get; set; }
    }
}
