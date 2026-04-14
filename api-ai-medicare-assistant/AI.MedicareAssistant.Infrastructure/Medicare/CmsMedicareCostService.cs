using System.Net.Http.Json;
using System.Text.Json;
using Domain.Models;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Medicare;

/// <summary>
/// Queries the CMS Medicare Part D Drug Spending open data API (data.cms.gov)
/// to retrieve real cost estimates for a given drug name.
/// </summary>
public class CmsMedicareCostService : IMedicareCostService
{
    private readonly HttpClient _http;
    private readonly ILogger<CmsMedicareCostService> _logger;
    private readonly string _baseUrl;

    public CmsMedicareCostService(
        HttpClient http,
        IConfiguration config,
        ILogger<CmsMedicareCostService> logger)
    {
        _http = http;
        _logger = logger;

        // CMS SOCRATA Open Data API for Medicare Part D Spending by Drug
        // Dataset: "Medicare Part D Drug Spending Dashboard & Data"
        // Configurable so it can be swapped for newer dataset versions
        _baseUrl = config["CMS:MedicarePartDSpendingUrl"]
            ?? "https://data.cms.gov/resource/7jry-e56r.json";
    }

    public async Task<MedicareCostEstimate?> GetCostEstimate(string drugName)
    {
        if (string.IsNullOrWhiteSpace(drugName))
            return null;

        try
        {
            // Query by brand name OR generic name (case-insensitive via upper())
            var sanitized = drugName.Trim().Replace("'", "''");
            var query = Uri.EscapeDataString(
                $"upper(brnd_name) like '%{sanitized.ToUpperInvariant()}%' " +
                $"OR upper(gnrc_name) like '%{sanitized.ToUpperInvariant()}%'");

            var url = $"{_baseUrl}?$where={query}&$limit=1&$order=tot_spndng DESC";

            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CMS API returned {Status} for drug '{Drug}'",
                    response.StatusCode, drugName);
                return null;
            }

            var records = await response.Content.ReadFromJsonAsync<List<CmsSpendingRecord>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (records is not { Count: > 0 })
                return null;

            var r = records[0];

            return new MedicareCostEstimate
            {
                Source = "CMS Medicare Part D Spending Data",
                DataYear = r.Tot_Spndng_Yr ?? "",
                DrugName = $"{r.Brnd_Name} ({r.Gnrc_Name})",
                TotalClaims = ParseInt(r.Tot_Clms),
                TotalBeneficiaries = ParseInt(r.Tot_Benes),
                AverageCostPerClaim = ParseDecimal(r.Avg_Spnd_Per_Clm),
                AverageMedicarePaymentPerClaim = ParseDecimal(r.Avg_Mdcr_Pymt_Per_Clm),
                AverageBeneficiaryCostShare = ParseDecimal(r.Avg_Bene_Cost_Shr),
                TotalSpending = ParseDecimal(r.Tot_Spndng),
                AverageSpendingPerBeneficiary = ParseDecimal(r.Avg_Spnd_Per_Bene)
            };
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("CMS API request timed out for drug '{Drug}'", drugName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch CMS data for drug '{Drug}'", drugName);
            return null;
        }
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, out var result) ? result : null;

    /// <summary>
    /// Represents a row from the CMS Medicare Part D Spending by Drug dataset.
    /// Field names match the SOCRATA column identifiers.
    /// </summary>
    private class CmsSpendingRecord
    {
        public string? Brnd_Name { get; set; }
        public string? Gnrc_Name { get; set; }
        public string? Tot_Spndng_Yr { get; set; }
        public string? Tot_Clms { get; set; }
        public string? Tot_Benes { get; set; }
        public string? Tot_Spndng { get; set; }
        public string? Avg_Spnd_Per_Clm { get; set; }
        public string? Avg_Mdcr_Pymt_Per_Clm { get; set; }
        public string? Avg_Bene_Cost_Shr { get; set; }
        public string? Avg_Spnd_Per_Bene { get; set; }
    }
}
