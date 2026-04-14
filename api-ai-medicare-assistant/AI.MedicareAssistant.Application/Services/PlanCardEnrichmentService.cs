using Application.DTOs;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Computes derived display fields for plan recommendation cards.
/// Pure computation — no I/O, no DI.
/// </summary>
public static class PlanCardEnrichmentService
{
    public static EnrichedPartDCard EnrichPartD(
        RecommendationListItem plan,
        PartDPlanRecommendationResponse response,
        List<string> selectedPharmacyNumbers,
        int totalDrugs)
    {
        var rec = plan.PharmacyWiseRecommendations.FirstOrDefault();
        var networkCount = rec?.PharmacyNetworks
            .Count(pn => selectedPharmacyNumbers.Contains(pn.PharmacyNumber)) ?? 0;
        var unavailableCount = rec?.UnavailableDrugs?.Count ?? 0;

        return new EnrichedPartDCard
        {
            PlanIdDisplay = $"{plan.ContractId}-{plan.PlanId}-{plan.SegmentId}",
            InsuranceCarrier = response.ContractIdCarrierMap.TryGetValue(plan.ContractId, out var carrier)
                ? carrier : "",
            PartDSurcharge = response.PartDPremiumSurcharge,
            PrescriptionOOP = rec?.TotalPrescriptionCost ?? 0,
            PharmaciesInNetwork = networkCount,
            TotalSelectedPharmacies = selectedPharmacyNumbers.Count,
            DrugsCovered = totalDrugs - unavailableCount,
            TotalDrugs = totalDrugs,
        };
    }

    public static EnrichedMedigapCard EnrichMedigap(
        MedigapPlanQuote quote,
        MedigapPlanQuotesResponse response)
    {
        var carrierName = "";
        if (quote.Naic != null && response.ContractIdCarrierMap != null)
        {
            response.ContractIdCarrierMap.TryGetValue(quote.Naic, out carrierName);
            carrierName ??= "";
        }

        return new EnrichedMedigapCard
        {
            PremiumMonthly = (double)(quote.Rate?.Month ?? 0) / 100.0,
            PremiumAnnual = (double)(quote.Rate?.Annual ?? 0) / 100.0,
            InsuranceCarrier = carrierName,
            PartBSurcharge = (double)(quote.PartBPremiumSurcharge ?? 0),
            HealthcareOOP = (double)(quote.PartBServiceOOP ?? 0),
            RemainingMonths = quote.MonthsUsedForExpenseCalc ?? 0,
        };
    }

    public static EnrichedMACard EnrichMA(
        RecommendationListItem plan,
        PartDPlanRecommendationResponse response,
        List<string> selectedPharmacyNumbers,
        int totalDrugs)
    {
        var rec = plan.PharmacyWiseRecommendations.FirstOrDefault();
        var surcharges = response.PartBPremiumSurcharge + response.PartDPremiumSurcharge;
        var networkCount = rec?.PharmacyNetworks
            .Count(pn => selectedPharmacyNumbers.Contains(pn.PharmacyNumber)) ?? 0;
        var unavailableCount = rec?.UnavailableDrugs?.Count ?? 0;

        return new EnrichedMACard
        {
            PlanIdDisplay = $"{plan.ContractId}-{plan.PlanId}-{plan.SegmentId}",
            InsuranceCarrier = response.ContractIdCarrierMap.TryGetValue(plan.ContractId, out var carrier)
                ? carrier : "",
            Surcharges = surcharges,
            PrescriptionOOP = rec?.TotalPrescriptionCost ?? 0,
            HealthcareOOP = rec?.PartAandBBenefitServiceCost ?? 0,
            HasPrescriptionDrug = rec?.PrescriptionDrugCovered ?? false,
            PharmaciesInNetwork = networkCount,
            TotalSelectedPharmacies = selectedPharmacyNumbers.Count,
            DrugsCovered = totalDrugs - unavailableCount,
            TotalDrugs = totalDrugs,
        };
    }
}
