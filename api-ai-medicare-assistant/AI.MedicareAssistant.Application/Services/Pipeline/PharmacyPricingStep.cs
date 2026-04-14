using System.Globalization;
using Domain.Interfaces;
using Domain.Models;
using Domain.Models.Pharmacy;
using Microsoft.Extensions.Logging;

namespace Application.Services.Pipeline;

/// <summary>
/// Step 5: Builds DrugPricingInput from enriched drug data
/// and fetches nearby pharmacy pricing when a ZIP code is available.
/// </summary>
public class PharmacyPricingStep : IDrugAnalysisStep
{
    private readonly IPharmacyPricingService _pharmacy;
    private readonly ILogger<PharmacyPricingStep> _logger;

    public int Order => 5;

    public PharmacyPricingStep(IPharmacyPricingService pharmacy, ILogger<PharmacyPricingStep> logger)
    {
        _pharmacy = pharmacy;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(DrugAnalysisResult result, AnalysisContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ZipCode))
            return true;

        try
        {
            var drugInputs = result.Drugs
                .Where(d => !string.IsNullOrWhiteSpace(d.RxNormId))
                .Select(d => new DrugPricingInput(
                    RxCui:         d.RxNormId!,
                    DrugName:      d.NormalizedDrugName,
                    Ndc:           d.NdcCodes?.FirstOrDefault(),
                    RetailPrice:   ParsePriceString(d.EstimatedRetailCostUSD),
                    MedicarePrice: ParsePriceString(d.EstimatedMedicarePartDCostUSD),
                    GenericPrice:  ParsePriceString(d.MedicareNegotiatedPriceUSD)
                ))
                .ToList();

            result.NearbyPharmacies = await _pharmacy
                .GetPharmaciesWithPricingAsync(context.ZipCode, drugInputs);

            _logger.LogInformation(
                "Pharmacy pricing complete. {Count} pharmacies returned for ZIP {Zip}",
                result.NearbyPharmacies.Count, context.ZipCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pharmacy pricing failed — continuing without pharmacy data");
            result.NearbyPharmacies = [];
        }

        return true;
    }

    /// <summary>
    /// Parses AI price strings like "$500 - $550/month" into a decimal
    /// by taking the lower bound of any range.
    /// </summary>
    internal static decimal? ParsePriceString(string? priceStr)
    {
        if (string.IsNullOrWhiteSpace(priceStr)) return null;

        var cleaned = priceStr
            .Replace("$", "")
            .Replace("/month", "")
            .Replace("/year", "")
            .Replace("/mo", "")
            .Trim();

        var parts = cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && decimal.TryParse(parts[0].Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }
}
