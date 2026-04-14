using Domain.Interfaces;
using Domain.Models.Pharmacy;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Orchestrates plan-aware pharmacy search: fetches nearby pharmacies,
/// overlays plan copay/formulary data, and flags preferred networks.
/// </summary>
public class PlanPharmacyService : IPlanPharmacyService
{
    private readonly IPharmacyPricingService _pharmacyService;
    private readonly ILogger<PlanPharmacyService> _logger;

    public PlanPharmacyService(
        IPharmacyPricingService pharmacyService,
        ILogger<PlanPharmacyService> logger)
    {
        _pharmacyService = pharmacyService;
        _logger = logger;
    }

    public async Task<List<PharmacyWithPricing>> GetPlanPharmaciesAsync(
        string planId,
        string zipCode,
        IEnumerable<DrugPricingInput> drugs,
        IEnumerable<PlanDrugCoverageInput> planCoverages,
        CancellationToken cancellationToken = default)
    {
        var drugList = drugs.ToList();
        var coverageList = planCoverages.ToList();

        _logger.LogInformation(
            "Plan pharmacy search: planId={PlanId}, zip={Zip}, {DrugCount} drugs",
            planId, zipCode, drugList.Count);

        // 1. Get standard pharmacy results with retail pricing
        var pharmacies = await _pharmacyService.GetPharmaciesWithPricingAsync(
            zipCode, drugList, cancellationToken);

        if (pharmacies.Count == 0) return [];

        // 2. Overlay plan copay/formulary data onto each pharmacy's drug prices
        var enriched = pharmacies.Select(p => EnrichWithPlanData(p, coverageList, planId)).ToList();

        // 3. Sort by total plan copay (preferred pharmacies first, then by cost)
        enriched = enriched
            .OrderByDescending(p => p.IsPreferredNetwork ?? false)
            .ThenBy(p => p.TotalPlanCopay ?? decimal.MaxValue)
            .ToList();

        _logger.LogInformation(
            "Plan pharmacy search complete: {Count} pharmacies enriched for plan {PlanId}",
            enriched.Count, planId);

        return enriched;
    }

    private static PharmacyWithPricing EnrichWithPlanData(
        PharmacyWithPricing pharmacy,
        List<PlanDrugCoverageInput> coverages,
        string planId)
    {
        // Determine if this pharmacy is "preferred" based on type heuristics
        // (In production, this would come from CMS network data — here we use
        //  pharmacy type as a proxy: chain/retail pharmacies are more likely preferred)
        var isPreferred = IsLikelyPreferredPharmacy(pharmacy.Pharmacy);

        var enrichedDrugs = pharmacy.Drugs.Select(drug =>
        {
            var coverage = coverages.FirstOrDefault(c =>
                c.RxCui == drug.RxCui ||
                c.DrugName.Equals(drug.DrugName, StringComparison.OrdinalIgnoreCase));

            if (coverage is null)
                return drug;

            // Preferred pharmacies typically get lower copays
            var copay = coverage.MonthlyCopay;
            if (isPreferred && copay > 0)
                copay = Math.Round(copay * 0.8m, 2); // ~20% lower at preferred

            return drug with
            {
                PlanCopay = coverage.IsCovered ? copay : null,
                FormularyTier = coverage.FormularyTier,
                RequiresPriorAuth = coverage.RequiresPriorAuth,
                IsPreferredPharmacy = isPreferred
            };
        }).ToList();

        var totalPlanCopay = enrichedDrugs
            .Where(d => d.PlanCopay.HasValue)
            .Sum(d => d.PlanCopay!.Value);

        return pharmacy with
        {
            Drugs = enrichedDrugs,
            TotalPlanCopay = totalPlanCopay > 0 ? totalPlanCopay : null,
            IsPreferredNetwork = isPreferred
        };
    }

    private static bool IsLikelyPreferredPharmacy(PharmacyResult pharmacy)
    {
        var name = pharmacy.Name.ToUpperInvariant();
        var type = pharmacy.PharmacyType.ToUpperInvariant();

        // Major chains are typically preferred network pharmacies
        var preferredChains = new[] {
            "CVS", "WALGREENS", "WALMART", "RITE AID", "COSTCO",
            "KROGER", "TARGET", "SAFEWAY", "PUBLIX", "H-E-B",
            "ALBERTSONS", "SAM'S CLUB"
        };

        return preferredChains.Any(chain => name.Contains(chain)) ||
               type.Contains("COMMUNITY/RETAIL") ||
               type.Contains("CHAIN");
    }
}
