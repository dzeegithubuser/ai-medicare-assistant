using System.Collections.Concurrent;
using System.Text.Json;
using Domain.Interfaces;
using Domain.Models.Pharmacy;
using Infrastructure.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Pharmacy;

public class CmsPharmacyPricingService : IPharmacyPricingService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IChatClient _chatClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<CmsPharmacyPricingService> _logger;

    // Prevent concurrent identical NPI lookups (thundering herd)
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _zipLocks = new();

    private const string NpiBaseUrl = "https://npiregistry.cms.hhs.gov/api";
    private const int MaxPharmacies = 10;

    public CmsPharmacyPricingService(HttpClient http, IMemoryCache cache,
        IChatClient chatClient, PromptBuilder promptBuilder, ILogger<CmsPharmacyPricingService> logger)
    {
        _http = http;
        _cache = cache;
        _chatClient = chatClient;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<List<PharmacyWithPricing>> GetPharmaciesWithPricingAsync(
        string zipCode, IEnumerable<DrugPricingInput> drugs, CancellationToken cancellationToken = default)
    {
        var drugList = drugs.ToList();
        if (drugList.Count == 0) return [];

        // 1. Fetch pharmacies from NPI Registry (cached 7 days per zip)
        var pharmacies = await GetPharmaciesAsync(zipCode, cancellationToken);
        if (pharmacies.Count == 0) return [];

        var pharmacyList = pharmacies.Take(MaxPharmacies).ToList();

        // 2. Get AI-generated per-pharmacy pricing — AI knows each pharmacy's
        //    name and type, so it produces realistic price variation directly
        var result = await GetPerPharmacyPricingAsync(zipCode, pharmacyList, drugList);

        return result;
    }

    /// <summary>
    /// Calls the AI model to generate per-pharmacy drug pricing. The AI receives
    /// the actual pharmacy names and types from NPI, so it can price Walmart
    /// differently from CVS or a hospital pharmacy — no artificial multipliers needed.
    /// Falls back to uniform pricing from DrugPricingInput if the AI call fails.
    /// </summary>
    private async Task<List<PharmacyWithPricing>> GetPerPharmacyPricingAsync(
        string zipCode,
        List<PharmacyResult> pharmacies,
        List<DrugPricingInput> drugs)
    {
        // Cache key: zip + sorted NPIs + sorted RxCUIs
        var npiKey = string.Join(",", pharmacies.Select(p => p.NPI).OrderBy(x => x));
        var rxKey = string.Join(",", drugs.Select(d => d.RxCui).OrderBy(x => x));
        var cacheKey = $"phpricing:{zipCode}:{npiKey}:{rxKey}";

        if (_cache.TryGetValue(cacheKey, out List<PharmacyWithPricing>? cached) && cached != null)
            return cached;

        try
        {
            var drugDescriptions = string.Join("\n", drugs.Select(d =>
                $"  - {d.DrugName} (RxCUI: {d.RxCui})"));

            var pharmacyDescriptions = string.Join("\n", pharmacies.Select((p, i) =>
                $"  {i + 1}. \"{p.Name}\" (NPI: {p.NPI}, Type: {p.PharmacyType})"));

            // Build prompts via PromptBuilder
            var (systemPrompt, userPrompt) = _promptBuilder.BuildPharmacyPricing(new Dictionary<string, string>
            {
                ["{{DRUG_DESCRIPTIONS}}"] = drugDescriptions,
                ["{{ZIP_CODE}}"] = zipCode,
                ["{{PHARMACY_LIST}}"] = pharmacyDescriptions,
                ["{{PHARMACY_COUNT}}"] = pharmacies.Count.ToString()
            });

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            var response = await _chatClient.GetResponseAsync(messages);
            var raw = response.Text?.Trim() ?? "";

            // Strip any accidental markdown fences
            raw = raw.Replace("```json", "").Replace("```", "").Trim();

            var aiResult = JsonSerializer.Deserialize<List<AiPharmacyPricingDto>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (aiResult == null || aiResult.Count == 0)
                return BuildFallbackResult(pharmacies, drugs);

            // Map AI response back to PharmacyWithPricing using NPI as the join key
            var pharmacyByNpi = pharmacies.ToDictionary(p => p.NPI);
            var result = new List<PharmacyWithPricing>();

            foreach (var aiPharmacy in aiResult)
            {
                if (!pharmacyByNpi.TryGetValue(aiPharmacy.Npi, out var pharmacy))
                    continue;

                var drugPrices = aiPharmacy.Drugs.Select(d => new DrugPrice
                {
                    DrugName      = d.DrugName ?? d.RxCui,
                    Ndc           = d.Ndc ?? string.Empty,
                    RxCui         = d.RxCui,
                    RetailPrice   = d.RetailPrice,
                    MedicarePrice = d.MedicarePrice,
                    GenericPrice  = d.GenericPrice
                }).ToList();

                result.Add(new PharmacyWithPricing
                {
                    Pharmacy          = pharmacy,
                    Drugs             = drugPrices,
                    TotalRetailCost   = drugPrices.Sum(d => d.RetailPrice ?? 0),
                    TotalMedicareCost = drugPrices.All(d => d.MedicarePrice == null)
                                            ? null
                                            : drugPrices.Sum(d => d.MedicarePrice ?? 0),
                    TotalGenericCost  = drugPrices.All(d => d.GenericPrice == null)
                                            ? null
                                            : drugPrices.Sum(d => d.GenericPrice ?? 0),
                });
            }

            // Include any pharmacies the AI missed — use fallback prices
            var coveredNpis = result.Select(r => r.Pharmacy.NPI).ToHashSet();
            var fallbackDrugs = BuildFallbackDrugPrices(drugs);
            foreach (var pharmacy in pharmacies.Where(p => !coveredNpis.Contains(p.NPI)))
            {
                result.Add(new PharmacyWithPricing
                {
                    Pharmacy          = pharmacy,
                    Drugs             = fallbackDrugs,
                    TotalRetailCost   = fallbackDrugs.Sum(d => d.RetailPrice ?? 0),
                    TotalMedicareCost = fallbackDrugs.All(d => d.MedicarePrice == null)
                                            ? null
                                            : fallbackDrugs.Sum(d => d.MedicarePrice ?? 0),
                    TotalGenericCost  = fallbackDrugs.All(d => d.GenericPrice == null)
                                            ? null
                                            : fallbackDrugs.Sum(d => d.GenericPrice ?? 0),
                });
            }

            result = result.OrderBy(p => p.TotalRetailCost).ToList();

            // Cache for 30 days — prices are market estimates, stable
            _cache.Set(cacheKey, result, TimeSpan.FromDays(30));

            _logger.LogInformation(
                "Per-pharmacy AI pricing complete. {PharmacyCount} pharmacies, {DrugCount} drugs for ZIP {Zip}",
                result.Count, drugs.Count, zipCode);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Per-pharmacy AI pricing failed — using fallback prices");
            return BuildFallbackResult(pharmacies, drugs);
        }
    }

    /// <summary>
    /// Builds uniform PharmacyWithPricing list using prices from DrugPricingInput.
    /// Used when the AI pricing call fails entirely.
    /// </summary>
    private static List<PharmacyWithPricing> BuildFallbackResult(
        List<PharmacyResult> pharmacies, List<DrugPricingInput> drugs)
    {
        var fallbackDrugs = BuildFallbackDrugPrices(drugs);
        return pharmacies.Select(pharmacy => new PharmacyWithPricing
        {
            Pharmacy          = pharmacy,
            Drugs             = fallbackDrugs,
            TotalRetailCost   = fallbackDrugs.Sum(d => d.RetailPrice ?? 0),
            TotalMedicareCost = fallbackDrugs.All(d => d.MedicarePrice == null)
                                    ? null
                                    : fallbackDrugs.Sum(d => d.MedicarePrice ?? 0),
            TotalGenericCost  = fallbackDrugs.All(d => d.GenericPrice == null)
                                    ? null
                                    : fallbackDrugs.Sum(d => d.GenericPrice ?? 0),
        }).OrderBy(p => p.TotalRetailCost).ToList();
    }

    private static List<DrugPrice> BuildFallbackDrugPrices(List<DrugPricingInput> drugs) =>
        drugs.Select(d => new DrugPrice
        {
            DrugName      = d.DrugName,
            Ndc           = d.Ndc ?? string.Empty,
            RxCui         = d.RxCui,
            RetailPrice   = d.RetailPrice,
            MedicarePrice = d.MedicarePrice,
            GenericPrice  = d.GenericPrice
        }).ToList();

    // Internal DTOs for AI response deserialization
    private sealed class AiPharmacyPricingDto
    {
        public string Npi { get; set; } = "";
        public List<AiDrugPriceDto> Drugs { get; set; } = [];
    }

    private sealed class AiDrugPriceDto
    {
        public string RxCui       { get; set; } = "";
        public string? DrugName   { get; set; }
        public string? Ndc        { get; set; }
        public decimal? RetailPrice   { get; set; }
        public decimal? MedicarePrice { get; set; }
        public decimal? GenericPrice  { get; set; }
    }

    public async Task<List<PharmacyResult>> GetNearbyPharmaciesAsync(
        string zipCode, CancellationToken cancellationToken = default)
    {
        return await GetPharmaciesAsync(zipCode, cancellationToken);
    }

    private async Task<List<PharmacyResult>> GetPharmaciesAsync(string zipCode, CancellationToken ct)
    {
        var cacheKey = $"npi_pharmacies_{zipCode}";

        if (_cache.TryGetValue(cacheKey, out List<PharmacyResult>? cached) && cached != null)
            return cached;

        // SemaphoreSlim per zip to prevent thundering herd
        var semaphore = _zipLocks.GetOrAdd(zipCode, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(cacheKey, out cached) && cached != null)
                return cached;

            var encodedZip = Uri.EscapeDataString(zipCode.Trim());
            var url = $"{NpiBaseUrl}/?version=2.1&taxonomy_description=pharmacy&postal_code={encodedZip}&limit={MaxPharmacies}";

            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NPI Registry returned {Status} for zip {ZipCode}", response.StatusCode, zipCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            var pharmacies = new List<PharmacyResult>();

            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var result in results.EnumerateArray())
                {
                    // Parse organization name — prefer "Doing Business As" (DBA) name
                    // since it's user-recognizable (e.g. "WALGREENS #12423" vs "WALGREEN CO")
                    var name = "";
                    var legalName = "";
                    var enumerationDate = "";

                    if (result.TryGetProperty("basic", out var basic))
                    {
                        legalName = basic.TryGetProperty("organization_name", out var orgName)
                            ? orgName.GetString() ?? ""
                            : basic.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                        name = legalName;

                        // Skip inactive pharmacies
                        if (basic.TryGetProperty("status", out var status) &&
                            status.GetString() != "A")
                            continue;

                        if (basic.TryGetProperty("enumeration_date", out var enumDate))
                            enumerationDate = enumDate.GetString() ?? "";
                    }

                    // Use DBA name if available (more recognizable to users)
                    if (result.TryGetProperty("other_names", out var otherNames))
                    {
                        foreach (var otherName in otherNames.EnumerateArray())
                        {
                            if (otherName.TryGetProperty("type", out var type) &&
                                type.GetString() == "Doing Business As" &&
                                otherName.TryGetProperty("organization_name", out var dbaName))
                            {
                                var dba = dbaName.GetString();
                                if (!string.IsNullOrEmpty(dba))
                                {
                                    name = dba;
                                    break;
                                }
                            }
                        }
                    }

                    // Extract primary taxonomy description (pharmacy type)
                    var pharmacyType = "";
                    if (result.TryGetProperty("taxonomies", out var taxonomies))
                    {
                        foreach (var tax in taxonomies.EnumerateArray())
                        {
                            if (tax.TryGetProperty("primary", out var primary) && primary.GetBoolean() &&
                                tax.TryGetProperty("desc", out var desc))
                            {
                                pharmacyType = desc.GetString() ?? "";
                                break;
                            }
                        }
                    }

                    // Get practice location address (prefer LOCATION over MAILING)
                    var address = "";
                    var addressLine2 = "";
                    var city = "";
                    var state = "";
                    var zip = "";
                    var phone = "";
                    var fax = "";

                    if (result.TryGetProperty("addresses", out var addresses))
                    {
                        // Prefer practice location (address_purpose = "LOCATION")
                        JsonElement? locationAddr = null;
                        foreach (var addr in addresses.EnumerateArray())
                        {
                            if (addr.TryGetProperty("address_purpose", out var purpose) &&
                                purpose.GetString() == "LOCATION")
                            {
                                locationAddr = addr;
                                break;
                            }
                        }

                        var addrEl = locationAddr ?? (addresses.GetArrayLength() > 0 ? addresses[0] : (JsonElement?)null);
                        if (addrEl.HasValue)
                        {
                            address = addrEl.Value.TryGetProperty("address_1", out var a1) ? a1.GetString() ?? "" : "";
                            addressLine2 = addrEl.Value.TryGetProperty("address_2", out var a2) ? a2.GetString() ?? "" : "";
                            city = addrEl.Value.TryGetProperty("city", out var c) ? c.GetString() ?? "" : "";
                            state = addrEl.Value.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";
                            phone = addrEl.Value.TryGetProperty("telephone_number", out var t) ? t.GetString() ?? "" : "";
                            fax = addrEl.Value.TryGetProperty("fax_number", out var f) ? f.GetString() ?? "" : "";

                            // Handle both "80113-3726" and "801133726" formats → always extract 5-digit zip
                            var rawZip = addrEl.Value.TryGetProperty("postal_code", out var z) ? z.GetString() ?? "" : "";
                            zip = rawZip.Replace("-", "");
                            if (zip.Length > 5) zip = zip[..5];
                        }
                    }

                    // NPI number field is a JSON string, not a number
                    var npi = "";
                    if (result.TryGetProperty("number", out var npiNum))
                    {
                        npi = npiNum.ValueKind == JsonValueKind.String
                            ? npiNum.GetString() ?? ""
                            : npiNum.GetRawText();
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        pharmacies.Add(new PharmacyResult
                        {
                            NPI = npi,
                            Name = name,
                            LegalName = legalName,
                            Address = address,
                            AddressLine2 = addressLine2,
                            City = city,
                            State = state,
                            ZipCode = zip,
                            Phone = phone,
                            Fax = fax,
                            PharmacyType = pharmacyType,
                            EnumerationDate = enumerationDate
                        });
                    }
                }
            }

            // Cache for 7 days — pharmacy locations rarely change
            _cache.Set(cacheKey, pharmacies, TimeSpan.FromDays(7));
            return pharmacies;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("NPI Registry request timed out for zip {ZipCode}", zipCode);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch pharmacies for zip {ZipCode}", zipCode);
            return [];
        }
        finally
        {
            semaphore.Release();
        }
    }
}
