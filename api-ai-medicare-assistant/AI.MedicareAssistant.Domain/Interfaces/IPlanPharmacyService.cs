using Domain.Models.Pharmacy;

namespace Domain.Interfaces;

/// <summary>
/// Provides plan-aware pharmacy search — enriches pharmacy results with
/// formulary copays, tier placements, and preferred network status
/// based on a selected Medicare plan.
/// </summary>
public interface IPlanPharmacyService
{
    /// <summary>
    /// Searches nearby pharmacies and overlays plan-specific copay pricing
    /// from the selected plan's formulary data.
    /// </summary>
    Task<List<PharmacyWithPricing>> GetPlanPharmaciesAsync(
        string planId,
        string zipCode,
        IEnumerable<DrugPricingInput> drugs,
        IEnumerable<PlanDrugCoverageInput> planCoverages,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Drug coverage from the selected plan, passed from the frontend.
/// </summary>
public record PlanDrugCoverageInput(
    string RxCui,
    string DrugName,
    int FormularyTier,
    decimal MonthlyCopay,
    bool IsCovered,
    bool RequiresPriorAuth
);
