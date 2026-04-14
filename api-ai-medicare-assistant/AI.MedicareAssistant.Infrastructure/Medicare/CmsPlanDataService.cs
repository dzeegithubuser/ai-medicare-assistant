using System.Net.Http.Json;
using System.Text.Json;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Medicare;

/// <summary>
/// Queries CMS SOCRATA open data APIs for Medicare plan info and formulary data.
/// Datasets:
///   - Plan info/premiums: Medicare Plan Finder landscape (data.cms.gov)
///   - Formulary/tier data: Plan Benefit Package formulary (data.cms.gov)
/// Falls back gracefully when CMS data is unavailable.
/// </summary>
public class CmsPlanDataService : ICmsPlanDataService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CmsPlanDataService> _logger;
    private readonly string _planInfoUrl;
    private readonly string _formularyUrl;

    public CmsPlanDataService(
        HttpClient http,
        IMemoryCache cache,
        IConfiguration config,
        ILogger<CmsPlanDataService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;

        // CMS Part D Plan Landscape dataset — premiums, deductibles by state/county
        _planInfoUrl = config["CMS:PlanLandscapeUrl"]
            ?? "https://data.cms.gov/resource/yjmm-q4z2.json";

        // CMS Part D Formulary data — tier placement & restrictions
        _formularyUrl = config["CMS:FormularyUrl"]
            ?? "https://data.cms.gov/resource/v6p3-p4zt.json";
    }

    public async Task<List<CmsPlanInfo>> GetPlansForAreaAsync(
        string stateCode,
        string countyName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stateCode) || string.IsNullOrWhiteSpace(countyName))
            return [];

        var cacheKey = $"cms-plans:{stateCode}:{countyName}";
        if (_cache.TryGetValue(cacheKey, out List<CmsPlanInfo>? cached) && cached is not null)
            return cached;

        try
        {
            var sanitizedCounty = countyName.Trim().Replace("'", "''").ToUpperInvariant();
            var sanitizedState = stateCode.Trim().ToUpperInvariant();

            var query = Uri.EscapeDataString(
                $"upper(state) = '{sanitizedState}' AND upper(county) like '%{sanitizedCounty}%'");

            var url = $"{_planInfoUrl}?$where={query}&$limit=50&$order=monthly_premium ASC";

            var response = await _http.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CMS Plan Landscape API returned {Status} for {State}/{County}",
                    response.StatusCode, stateCode, countyName);
                return [];
            }

            var records = await response.Content.ReadFromJsonAsync<List<CmsPlanLandscapeRecord>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (records is not { Count: > 0 })
            {
                _logger.LogInformation("No CMS plan data found for {State}/{County}", stateCode, countyName);
                return [];
            }

            var plans = records.Select(r => new CmsPlanInfo
            {
                ContractId = r.Contract_Id ?? "",
                PlanId = r.Plan_Id ?? "",
                PlanName = r.Plan_Name ?? "",
                OrganizationName = r.Organization_Name ?? "",
                PlanType = r.Plan_Type ?? "",
                MonthlyPremium = ParseDecimal(r.Monthly_Premium) ?? 0m,
                AnnualDeductible = ParseDecimal(r.Annual_Deductible) ?? 0m,
                StarRating = ParseDecimal(r.Overall_Star_Rating) ?? 0m,
                StateCode = r.State ?? "",
                CountyName = r.County ?? ""
            }).ToList();

            // Cache for 7 days — plan landscape data changes infrequently
            _cache.Set(cacheKey, plans, TimeSpan.FromDays(7));

            _logger.LogInformation("Fetched {Count} CMS plans for {State}/{County}",
                plans.Count, stateCode, countyName);

            return plans;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("CMS Plan Landscape API timed out for {State}/{County}", stateCode, countyName);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch CMS plan data for {State}/{County}", stateCode, countyName);
            return [];
        }
    }

    public async Task<List<CmsFormularyEntry>> GetFormularyEntriesAsync(
        string contractId,
        IEnumerable<string> rxCuis,
        CancellationToken cancellationToken = default)
    {
        var rxCuiList = rxCuis.ToList();
        if (string.IsNullOrWhiteSpace(contractId) || rxCuiList.Count == 0)
            return [];

        var rxKey = string.Join(",", rxCuiList.OrderBy(x => x));
        var cacheKey = $"cms-formulary:{contractId}:{rxKey}";
        if (_cache.TryGetValue(cacheKey, out List<CmsFormularyEntry>? cached) && cached is not null)
            return cached;

        try
        {
            var sanitizedContract = contractId.Trim().Replace("'", "''");
            var rxCuiIn = string.Join("','", rxCuiList.Select(r => r.Trim().Replace("'", "''")));

            var query = Uri.EscapeDataString(
                $"contract_id = '{sanitizedContract}' AND rxcui IN ('{rxCuiIn}')");

            var url = $"{_formularyUrl}?$where={query}&$limit=100";

            var response = await _http.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CMS Formulary API returned {Status} for contract {Contract}",
                    response.StatusCode, contractId);
                return [];
            }

            var records = await response.Content.ReadFromJsonAsync<List<CmsFormularyRecord>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (records is not { Count: > 0 })
                return [];

            var entries = records.Select(r => new CmsFormularyEntry
            {
                RxCui = r.Rxcui ?? "",
                DrugName = r.Drug_Name ?? "",
                ContractId = r.Contract_Id ?? "",
                FormularyTier = ParseInt(r.Tier_Level) ?? 3,
                RequiresPriorAuth = r.Prior_Auth?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false,
                HasQuantityLimit = r.Quantity_Limit?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false,
                HasStepTherapy = r.Step_Therapy?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false
            }).ToList();

            // Cache for 7 days
            _cache.Set(cacheKey, entries, TimeSpan.FromDays(7));

            return entries;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("CMS Formulary API timed out for contract {Contract}", contractId);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch CMS formulary data for contract {Contract}", contractId);
            return [];
        }
    }

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, out var result) ? result : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;

    // ── CMS SOCRATA response records ──

    private class CmsPlanLandscapeRecord
    {
        public string? Contract_Id { get; set; }
        public string? Plan_Id { get; set; }
        public string? Plan_Name { get; set; }
        public string? Organization_Name { get; set; }
        public string? Plan_Type { get; set; }
        public string? Monthly_Premium { get; set; }
        public string? Annual_Deductible { get; set; }
        public string? Overall_Star_Rating { get; set; }
        public string? State { get; set; }
        public string? County { get; set; }
    }

    private class CmsFormularyRecord
    {
        public string? Contract_Id { get; set; }
        public string? Rxcui { get; set; }
        public string? Drug_Name { get; set; }
        public string? Tier_Level { get; set; }
        public string? Prior_Auth { get; set; }
        public string? Quantity_Limit { get; set; }
        public string? Step_Therapy { get; set; }
    }
}
