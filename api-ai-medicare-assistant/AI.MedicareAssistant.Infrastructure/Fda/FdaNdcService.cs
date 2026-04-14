using System.Text.Json;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Fda;

/// <summary>
/// Queries the openFDA NDC Directory API to resolve package-level details
/// (package size, package type) for a given NDC code.
/// </summary>
public class FdaNdcService : IFdaNdcService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FdaNdcService> _logger;
    private const string BaseUrl = "https://api.fda.gov/drug/ndc.json";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);

    public FdaNdcService(HttpClient http, IMemoryCache cache, ILogger<FdaNdcService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<NdcPackageInfo?> GetPackageInfo(string ndcCode)
    {
        if (string.IsNullOrWhiteSpace(ndcCode))
            return null;

        var cacheKey = $"fda-ndc:{ndcCode}";
        if (_cache.TryGetValue(cacheKey, out NdcPackageInfo? cached))
            return cached;

        try
        {
            // openFDA expects the product_ndc (first two segments: XXXXX-XXXX)
            var productNdc = ToProductNdc(ndcCode);
            var url = $"{BaseUrl}?search=product_ndc:\"{Uri.EscapeDataString(productNdc)}\"&limit=1";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("FDA NDC API returned {Status} for NDC '{Ndc}'",
                    response.StatusCode, ndcCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = ParsePackageInfo(json, ndcCode);

            _cache.Set(cacheKey, result, CacheDuration);
            return result;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("FDA NDC API request timed out for NDC '{Ndc}'", ndcCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch FDA NDC data for '{Ndc}'", ndcCode);
            return null;
        }
    }

    public async Task<List<NdcPackageInfo>> GetPackageInfoBatch(List<string> ndcCodes)
    {
        var tasks = ndcCodes.Select(async ndc =>
        {
            var info = await GetPackageInfo(ndc);
            return info;
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).Cast<NdcPackageInfo>().ToList();
    }

    private NdcPackageInfo? ParsePackageInfo(string json, string ndcCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            var product = results[0];

            if (!product.TryGetProperty("packaging", out var packagingArray) || packagingArray.GetArrayLength() == 0)
                return null;

            // Find the package that matches this specific NDC
            // openFDA packaging entries have "package_ndc" field
            foreach (var pkg in packagingArray.EnumerateArray())
            {
                var packageNdc = pkg.TryGetProperty("package_ndc", out var pNdc)
                    ? pNdc.GetString() ?? ""
                    : "";

                // Normalize both NDCs for comparison (remove leading zeros, dashes)
                if (!NdcMatches(packageNdc, ndcCode))
                    continue;

                var description = pkg.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? ""
                    : "";

                // Parse description like "100 TABLET in 1 BLISTER PACK" or "60 TABLET in 1 BOTTLE"
                var (size, type) = ParseDescription(description);

                return new NdcPackageInfo
                {
                    NdcCode = ndcCode,
                    PackageDescription = description,
                    PackageSize = size,
                    PackageType = type
                };
            }

            // If no exact match found, return the first packaging entry
            var firstPkg = packagingArray[0];
            var firstDesc = firstPkg.TryGetProperty("description", out var fDesc)
                ? fDesc.GetString() ?? ""
                : "";
            var (firstSize, firstType) = ParseDescription(firstDesc);

            return new NdcPackageInfo
            {
                NdcCode = ndcCode,
                PackageDescription = firstDesc,
                PackageSize = firstSize,
                PackageType = firstType
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse FDA NDC response for '{Ndc}'", ndcCode);
            return null;
        }
    }

    /// <summary>
    /// Extracts product NDC (first two segments) from a full NDC code.
    /// "00003-0893-21" → "0003-0893"  (openFDA uses shorter format without leading zeros on labeler)
    /// </summary>
    private static string ToProductNdc(string ndcCode)
    {
        var parts = ndcCode.Split('-');
        if (parts.Length >= 2)
            return $"{parts[0]}-{parts[1]}";
        return ndcCode;
    }

    /// <summary>
    /// Compares two NDC codes accounting for formatting differences.
    /// Strips dashes and leading zeros for comparison.
    /// </summary>
    private static bool NdcMatches(string ndc1, string ndc2)
    {
        if (string.IsNullOrWhiteSpace(ndc1) || string.IsNullOrWhiteSpace(ndc2))
            return false;

        var normalized1 = ndc1.Replace("-", "").TrimStart('0');
        var normalized2 = ndc2.Replace("-", "").TrimStart('0');
        return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses FDA package description like "60 TABLET in 1 BOTTLE" or "100 TABLET, FILM COATED in 1 BLISTER PACK".
    /// Returns (packageSize, packageType).
    /// </summary>
    private static (int size, string type) ParseDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return (0, "");

        // Pattern: "<count> <form> in <container_count> <container_type>"
        // Examples:
        //   "60 TABLET in 1 BOTTLE"
        //   "100 TABLET, FILM COATED in 1 BLISTER PACK"
        //   "473 mL in 1 BOTTLE"
        var parts = description.Split(" in ", StringSplitOptions.RemoveEmptyEntries);
        int size = 0;
        string type = "";

        if (parts.Length >= 1)
        {
            // Extract leading number from first part
            var sizeStr = new string(parts[0].TakeWhile(char.IsDigit).ToArray());
            int.TryParse(sizeStr, out size);
        }

        if (parts.Length >= 2)
        {
            // Extract container type — strip the leading count (e.g., "1 BOTTLE" → "BOTTLE")
            var containerPart = parts[1].Trim();
            var spaceIdx = containerPart.IndexOf(' ');
            type = spaceIdx >= 0 ? containerPart[(spaceIdx + 1)..].Trim() : containerPart;
        }

        return (size, type);
    }
}
