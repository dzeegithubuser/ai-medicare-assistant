using System.Text.Json.Serialization;

namespace Domain.Models.Pharmacy;

public record PharmacyResult
{
    [JsonPropertyName("npi")]
    public string NPI { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("legalName")]
    public string LegalName { get; init; } = "";

    [JsonPropertyName("address")]
    public string Address { get; init; } = "";

    [JsonPropertyName("addressLine2")]
    public string AddressLine2 { get; init; } = "";

    [JsonPropertyName("city")]
    public string City { get; init; } = "";

    [JsonPropertyName("state")]
    public string State { get; init; } = "";

    [JsonPropertyName("zipCode")]
    public string ZipCode { get; init; } = "";

    [JsonPropertyName("phone")]
    public string Phone { get; init; } = "";

    [JsonPropertyName("fax")]
    public string Fax { get; init; } = "";

    [JsonPropertyName("pharmacyType")]
    public string PharmacyType { get; init; } = "";

    [JsonPropertyName("enumerationDate")]
    public string EnumerationDate { get; init; } = "";
}

public record DrugPrice
{
    [JsonPropertyName("drugName")]
    public string DrugName { get; init; } = "";

    [JsonPropertyName("ndc")]
    public string Ndc { get; init; } = "";

    [JsonPropertyName("rxCui")]
    public string RxCui { get; init; } = "";

    [JsonPropertyName("retailPrice")]
    public decimal? RetailPrice { get; init; }

    [JsonPropertyName("medicarePrice")]
    public decimal? MedicarePrice { get; init; }

    [JsonPropertyName("genericPrice")]
    public decimal? GenericPrice { get; init; }

    // Plan-aware fields (Phase 3)
    [JsonPropertyName("planCopay")]
    public decimal? PlanCopay { get; init; }

    [JsonPropertyName("formularyTier")]
    public int? FormularyTier { get; init; }

    [JsonPropertyName("requiresPriorAuth")]
    public bool? RequiresPriorAuth { get; init; }

    [JsonPropertyName("isPreferredPharmacy")]
    public bool? IsPreferredPharmacy { get; init; }
}

public record PharmacyWithPricing
{
    [JsonPropertyName("pharmacy")]
    public PharmacyResult Pharmacy { get; init; } = new();

    [JsonPropertyName("drugs")]
    public List<DrugPrice> Drugs { get; init; } = [];

    [JsonPropertyName("totalRetailCost")]
    public decimal? TotalRetailCost { get; init; }

    [JsonPropertyName("totalMedicareCost")]
    public decimal? TotalMedicareCost { get; init; }

    [JsonPropertyName("totalGenericCost")]
    public decimal? TotalGenericCost { get; init; }

    // Plan-aware fields (Phase 3)
    [JsonPropertyName("totalPlanCopay")]
    public decimal? TotalPlanCopay { get; init; }

    [JsonPropertyName("isPreferredNetwork")]
    public bool? IsPreferredNetwork { get; init; }
}

/// <summary>
/// Input DTO passed to IPharmacyPricingService.
/// Carries drug identity + AI-estimated prices so the pricing service
/// does not need to re-fetch them from an external API.
/// </summary>
public record DrugPricingInput(
    string RxCui,
    string DrugName,
    string? Ndc,
    decimal? RetailPrice,
    decimal? MedicarePrice,
    decimal? GenericPrice
);
