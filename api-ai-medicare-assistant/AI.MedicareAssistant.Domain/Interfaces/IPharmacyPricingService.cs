using Domain.Models.Pharmacy;

namespace Domain.Interfaces;

public interface IPharmacyPricingService
{
    Task<List<PharmacyWithPricing>> GetPharmaciesWithPricingAsync(
        string zipCode,
        IEnumerable<DrugPricingInput> drugs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns nearby pharmacies by zip code without any pricing data.
    /// Uses NPI Registry only.
    /// </summary>
    Task<List<PharmacyResult>> GetNearbyPharmaciesAsync(
        string zipCode,
        CancellationToken cancellationToken = default);
}
