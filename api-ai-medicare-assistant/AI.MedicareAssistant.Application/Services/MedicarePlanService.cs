using Domain.Interfaces;
using Domain.Models;
using Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class MedicarePlanService : IMedicarePlanService
{
    private readonly ICountyLookupService _countyLookup;
    private readonly IPlanScoringAiService _aiScoring;
    private readonly ICmsPlanDataService _cmsPlanData;
    private readonly ProfileService _profileService;
    private readonly ILogger<MedicarePlanService> _logger;

    public MedicarePlanService(
        ICountyLookupService countyLookup,
        IPlanScoringAiService aiScoring,
        ICmsPlanDataService cmsPlanData,
        ProfileService profileService,
        ILogger<MedicarePlanService> logger)
    {
        _countyLookup = countyLookup;
        _aiScoring = aiScoring;
        _cmsPlanData = cmsPlanData;
        _profileService = profileService;
        _logger = logger;
    }

    public async Task<PlanRecommendationResult> RecommendPlansAsync(
        PlanRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting plan recommendation for user {UserId}, ZIP {Zip}, {DrugCount} drugs",
            request.UserId, request.ZipCode, request.RxCuis.Count);

        // Resolve county code from ZIP
        var countyCode = await _countyLookup.GetCountyCode(request.ZipCode);
        var countyName = await _countyLookup.GetCountyName(request.ZipCode) ?? "Unknown County";

        if (string.IsNullOrEmpty(countyCode))
        {
            _logger.LogWarning("No county mapping for ZIP {Zip}, using ZIP as county context", request.ZipCode);
            countyName = $"ZIP {request.ZipCode} area";
        }

        // Determine LIS eligibility
        var lisTier = DetermineLisTier(
            request.AnnualIncome,
            request.HouseholdSize,
            request.IncomeFilingStatus);

        _logger.LogInformation(
            "LIS determination: {LisTier} (income=${Income}, household={Size})",
            lisTier, request.AnnualIncome, request.HouseholdSize);

        // Call AI for plan scoring and ranking
        var result = await _aiScoring.ScorePlansAsync(request, countyName, cancellationToken);

        // Enrich AI-generated plans with real CMS data when available
        await EnrichWithCmsDataAsync(result, request, countyName, cancellationToken);

        // Apply LIS copay adjustments deterministically (AI generates standard copays)
        ApplyLisAdjustments(result, lisTier, request.AnnualIncome, request.HouseholdSize);

        // Override LIS fields with our deterministic computation
        result.LisEligible = lisTier != LisTier.None;
        result.LisTier = lisTier.ToString();

        if (result.LisEligible)
        {
            result.LisCallToAction = lisTier == LisTier.Full
                ? "You may qualify for Full Extra Help, which could reduce your drug copays to $0. " +
                  "Contact Social Security at 1-800-772-1213 or visit ssa.gov/medicare/part-d-extra-help to apply."
                : "You may qualify for Partial Extra Help, which could reduce your drug copays significantly. " +
                  "Contact Social Security at 1-800-772-1213 or visit ssa.gov/medicare/part-d-extra-help to apply.";
        }

        _logger.LogInformation(
            "Plan recommendation complete — {PlanCount} plans, LIS={LisTier}, recommended type={Type}",
            result.RankedPlans.Count, result.LisTier, result.RecommendedPlanType);

        // Compute pharmacy-specific cost breakdowns for each selected pharmacy
        if (request.SelectedPharmacies is { Count: > 0 })
        {
            ComputePharmacyCostBreakdowns(result, request.SelectedPharmacies);
        }

        return result;
    }

    /// <summary>
    /// Enriches AI-generated plan recommendations with real CMS data.
    /// Matches AI plans to CMS plans by name similarity and updates
    /// premiums, deductibles, star ratings, and formulary tiers.
    /// </summary>
    private async Task EnrichWithCmsDataAsync(
        PlanRecommendationResult result,
        PlanRecommendationRequest request,
        string countyName,
        CancellationToken cancellationToken)
    {
        // Derive state code from ZIP (first 3 digits → rough state mapping via FIPS)
        var stateCode = await _countyLookup.GetStateCode(request.ZipCode);
        if (string.IsNullOrEmpty(stateCode))
        {
            _logger.LogDebug("No state code for ZIP {Zip}, skipping CMS enrichment", request.ZipCode);
            return;
        }

        var cmsPlans = await _cmsPlanData.GetPlansForAreaAsync(stateCode, countyName, cancellationToken);
        if (cmsPlans.Count == 0)
        {
            _logger.LogDebug("No CMS plans found for {State}/{County}, AI data will be used as-is",
                stateCode, countyName);
            return;
        }

        _logger.LogInformation("Enriching {AiCount} AI plans with {CmsCount} CMS plans for {State}/{County}",
            result.RankedPlans.Count, cmsPlans.Count, stateCode, countyName);

        foreach (var aiPlan in result.RankedPlans)
        {
            // Match by plan name similarity (case-insensitive contains)
            var cmsPlan = cmsPlans.FirstOrDefault(c =>
                c.PlanName.Contains(aiPlan.PlanName, StringComparison.OrdinalIgnoreCase) ||
                aiPlan.PlanName.Contains(c.PlanName, StringComparison.OrdinalIgnoreCase) ||
                c.OrganizationName.Contains(aiPlan.InsuranceName, StringComparison.OrdinalIgnoreCase));

            if (cmsPlan is null) continue;

            // Override AI estimates with real CMS data
            aiPlan.MonthlyPremium = cmsPlan.MonthlyPremium;
            aiPlan.AnnualDeductible = cmsPlan.AnnualDeductible;
            aiPlan.StarRating = cmsPlan.StarRating > 0
                ? cmsPlan.StarRating.ToString("F1")
                : aiPlan.StarRating;

            // Recalculate total cost with real premium and deductible
            aiPlan.EstimatedAnnualTotalCost =
                (cmsPlan.MonthlyPremium * 12) + cmsPlan.AnnualDeductible + aiPlan.EstimatedAnnualDrugCost;

            // Update plan identifier with CMS contract/plan IDs
            if (!string.IsNullOrEmpty(cmsPlan.ContractId))
                aiPlan.PlanId = $"{cmsPlan.ContractId}-{cmsPlan.PlanId}";

            // Enrich formulary data if CMS has drug-level info
            if (!string.IsNullOrEmpty(cmsPlan.ContractId) && request.RxCuis.Count > 0)
            {
                var formularyEntries = await _cmsPlanData.GetFormularyEntriesAsync(
                    cmsPlan.ContractId, request.RxCuis, cancellationToken);

                if (formularyEntries.Count > 0)
                {
                    foreach (var coverage in aiPlan.DrugCoverages)
                    {
                        var cmsEntry = formularyEntries.FirstOrDefault(f =>
                            f.RxCui == coverage.RxCui);

                        if (cmsEntry is null) continue;

                        coverage.FormularyTier = cmsEntry.FormularyTier;
                        coverage.RequiresPriorAuth = cmsEntry.RequiresPriorAuth;
                        coverage.HasQuantityLimit = cmsEntry.HasQuantityLimit;
                        coverage.IsCovered = true;
                    }
                }
            }

            _logger.LogDebug("Enriched plan '{PlanName}' with CMS data: premium=${Premium}, deductible=${Deductible}",
                aiPlan.PlanName, cmsPlan.MonthlyPremium, cmsPlan.AnnualDeductible);
        }

        // Re-sort by estimated annual total cost after enrichment
        result.RankedPlans = result.RankedPlans
            .OrderBy(p => p.EstimatedAnnualTotalCost)
            .ToList();
    }

    /// <summary>
    /// Builds a PlanRecommendationRequest from the user's saved profile and drug list.
    /// Returns null if required profile data is missing.
    /// </summary>
    public async Task<PlanRecommendationRequest?> BuildRequestAsync(
        Guid userId,
        List<DrugSummary> drugSummaries,
        List<SelectedPharmacy>? selectedPharmacies,
        CancellationToken cancellationToken = default)
    {
        var profileResponse = await _profileService.GetProfileAsync(userId);
        var p = profileResponse.Profile;

        if (p is null || string.IsNullOrWhiteSpace(p.ZipCode))
        {
            _logger.LogWarning("Profile/address missing for user {UserId}, cannot build plan request", userId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(p.MagiTier))
        {
            _logger.LogWarning("Income data missing for user {UserId}, cannot build plan request", userId);
            return null;
        }

        // Derive age from DOB
        int? age = null;
        if (!string.IsNullOrWhiteSpace(p.DateOfBirth) &&
            DateTime.TryParse(p.DateOfBirth, out var dob))
        {
            age = CalculateAge(dob);
        }

        return new PlanRecommendationRequest(
            UserId: userId.ToString(),
            ZipCode: p.ZipCode,
            County: p.County,
            RxCuis: drugSummaries.Select(d => d.RxCui).ToList(),
            MagiTier: p.MagiTier,
            AnnualIncome: 0m,
            HouseholdSize: 1,
            IncomeFilingStatus: p.TaxFilingStatus,
            HasEmployerCoverage: false,
            DisabilityStatus: null,
            HasChronicCondition: false,
            ChronicConditionDetails: null,
            RetirementAge: null,
            Age: age,
            DrugSummaries: drugSummaries,
            SelectedPharmacies: selectedPharmacies
        );
    }

    /// <summary>
    /// Quick LIS check that only requires income data.
    /// </summary>
    public async Task<(bool Eligible, LisTier Tier)> CheckLisEligibilityAsync(Guid userId)
    {
        var profileResponse = await _profileService.GetProfileAsync(userId);
        var p = profileResponse.Profile;
        if (p is null)
            return (false, LisTier.None);

        var tier = DetermineLisTier(
            0m,
            1,
            p.TaxFilingStatus);

        return (tier != LisTier.None, tier);
    }

    /// <summary>
    /// Determines LIS (Low-Income Subsidy / Extra Help) tier based on 2025 FPL thresholds.
    /// Full: ≤135% FPL. Partial: ≤150% FPL.
    /// Household scaling: +$8,070 per additional member (full), +$11,640 (partial).
    /// </summary>
    internal static LisTier DetermineLisTier(decimal annualIncome, int householdSize, string filingStatus)
    {
        var size = Math.Max(1, householdSize);

        // 2025 thresholds (base for individual + per-additional-member scaling)
        var fullLimit = 22590m + Math.Max(0, size - 1) * 8070m;
        var partialLimit = 33240m + Math.Max(0, size - 1) * 11640m;

        // For married filing jointly, use household income thresholds
        // (already scaled by household size above)

        if (annualIncome <= fullLimit) return LisTier.Full;
        if (annualIncome <= partialLimit) return LisTier.Partial;
        return LisTier.None;
    }

    /// <summary>
    /// Applies LIS (Extra Help) copay adjustments deterministically based on 2025 Medicare rules.
    /// Full LIS below 100% FPL: $0 copay. Full LIS 100-135% FPL or Partial: $4.50 generic / $11.20 brand.
    /// Recalculates drug costs and total costs after adjustment.
    /// </summary>
    private static void ApplyLisAdjustments(
        PlanRecommendationResult result,
        LisTier lisTier,
        decimal annualIncome,
        int householdSize)
    {
        if (lisTier == LisTier.None) return;

        // 2025 FPL base for contiguous US: $15,060 + $5,380 per additional household member
        var fpl100 = 15_060m + Math.Max(0, householdSize - 1) * 5_380m;

        // Full LIS below 100% FPL → $0; Full LIS above 100% FPL or Partial → reduced copays
        var (genericCopay, brandCopay) = lisTier switch
        {
            LisTier.Full when annualIncome <= fpl100 => (0m, 0m),
            _ => (4.50m, 11.20m)
        };

        foreach (var plan in result.RankedPlans)
        {
            foreach (var drug in plan.DrugCoverages)
            {
                if (!drug.IsCovered) continue;

                // Tiers 1-2 = generic, Tiers 3+ = brand
                drug.MonthlyCopay = drug.FormularyTier <= 2 ? genericCopay : brandCopay;
            }

            plan.EstimatedAnnualDrugCost = plan.DrugCoverages
                .Where(d => d.IsCovered)
                .Sum(d => d.MonthlyCopay * 12);

            plan.EstimatedAnnualTotalCost =
                (plan.MonthlyPremium * 12) + plan.AnnualDeductible + plan.EstimatedAnnualDrugCost;
        }

        // Re-sort by total cost after LIS adjustments
        result.RankedPlans = result.RankedPlans
            .OrderBy(p => p.EstimatedAnnualTotalCost)
            .ToList();
    }

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }

    /// <summary>
    /// Computes a detailed cost breakdown per plan for each of the user's selected pharmacies.
    /// Preferred pharmacies get a ~20% copay discount.
    /// Uses the cheapest pharmacy's cost to set the plan total for sorting.
    /// </summary>
    private static void ComputePharmacyCostBreakdowns(
        PlanRecommendationResult result, List<SelectedPharmacy> pharmacies)
    {
        foreach (var plan in result.RankedPlans)
        {
            var breakdowns = new List<PlanCostBreakdown>();

            foreach (var pharmacy in pharmacies)
            {
                var isPreferred = IsLikelyPreferredPharmacy(pharmacy.Name, pharmacy.PharmacyType);

                var drugCopays = plan.DrugCoverages.Select(drug =>
                {
                    var copay = drug.MonthlyCopay;
                    var discounted = false;
                    if (isPreferred && copay > 0 && drug.IsCovered)
                    {
                        copay = Math.Round(copay * 0.8m, 2);
                        discounted = true;
                    }

                    return new DrugCopayDetail
                    {
                        DrugName = drug.DrugName,
                        RxCui = drug.RxCui,
                        FormularyTier = drug.FormularyTier,
                        MonthlyCopay = copay,
                        AnnualCopay = drug.IsCovered ? copay * 12 : 0m,
                        IsCovered = drug.IsCovered,
                        PreferredDiscount = discounted
                    };
                }).ToList();

                var annualDrugCopay = drugCopays.Sum(d => d.AnnualCopay);
                var annualPremium = plan.MonthlyPremium * 12;
                var annualDeductible = plan.AnnualDeductible;

                breakdowns.Add(new PlanCostBreakdown
                {
                    PharmacyName = pharmacy.Name,
                    PharmacyNpi = pharmacy.Npi,
                    IsPreferredPharmacy = isPreferred,
                    AnnualPremium = annualPremium,
                    AnnualDeductible = annualDeductible,
                    AnnualDrugCopay = annualDrugCopay,
                    AnnualTotal = annualPremium + annualDeductible + annualDrugCopay,
                    DrugCopays = drugCopays
                });
            }

            plan.CostBreakdowns = breakdowns.OrderBy(b => b.AnnualTotal).ToList();

            // Use the cheapest pharmacy's cost for the plan-level totals
            var best = plan.CostBreakdowns.First();
            plan.EstimatedAnnualDrugCost = best.AnnualDrugCopay;
            plan.EstimatedAnnualTotalCost = best.AnnualTotal;
        }

        // Re-sort by best pharmacy-aware total cost
        result.RankedPlans = result.RankedPlans
            .OrderBy(p => p.EstimatedAnnualTotalCost)
            .ToList();
    }

    private static bool IsLikelyPreferredPharmacy(string name, string pharmacyType)
    {
        var upperName = name.ToUpperInvariant();
        var upperType = pharmacyType.ToUpperInvariant();

        var preferredChains = new[]
        {
            "CVS", "WALGREENS", "WALMART", "RITE AID", "COSTCO",
            "KROGER", "TARGET", "SAFEWAY", "PUBLIX", "H-E-B",
            "ALBERTSONS", "SAM'S CLUB"
        };

        return preferredChains.Any(chain => upperName.Contains(chain)) ||
               upperType.Contains("COMMUNITY/RETAIL") ||
               upperType.Contains("CHAIN");
    }
}
