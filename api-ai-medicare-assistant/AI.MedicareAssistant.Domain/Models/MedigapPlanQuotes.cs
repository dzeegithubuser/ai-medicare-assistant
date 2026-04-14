using System.Text.Json.Serialization;

namespace Domain.Models;

// ───── REQUEST (query parameters) ─────

public class MedigapPlanQuotesRequest
{
    public string Zip5 { get; set; } = "";
    public string Gender { get; set; } = "";
    public int Tobacco { get; set; }
    public string BirthDate { get; set; } = "";   // MM-YYYY
    public string Plan { get; set; } = "G";
    public string County { get; set; } = "";
    public string TaxFilingStatus { get; set; } = "";
    public int MagiTier { get; set; }
    public int HealthProfile { get; set; }
    public string CoverageYear { get; set; } = "";
    public string? VersionId { get; set; }  // null | "AIVANTE" | "MEDICARE_GOV"
}

// ───── RESPONSE ─────

public class MedigapPlanQuotesResponse
{
    [JsonPropertyName("contractIdCarrierMap")]
    public Dictionary<string, string>? ContractIdCarrierMap { get; set; }

    [JsonPropertyName("deductible")]
    public decimal? Deductible { get; set; }

    [JsonPropertyName("planList")]
    public List<MedigapPlanQuote>? PlanList { get; set; }
}

public class MedigapPlanQuote
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("age")]
    public int? Age { get; set; }

    [JsonPropertyName("archive")]
    public object? Archive { get; set; }

    [JsonPropertyName("company_base")]
    public MedigapCompanyBase? CompanyBase { get; set; }

    [JsonPropertyName("contextual_data")]
    public MedigapContextualData? ContextualData { get; set; }

    [JsonPropertyName("discount_category")]
    public string? DiscountCategory { get; set; }

    [JsonPropertyName("discounts")]
    public List<MedigapDiscount>? Discounts { get; set; }

    [JsonPropertyName("e_app_link")]
    public string? EAppLink { get; set; }

    [JsonPropertyName("effective_date")]
    public DateTime? EffectiveDate { get; set; }

    [JsonPropertyName("expires_date")]
    public DateTime? ExpiresDate { get; set; }

    [JsonPropertyName("fees")]
    public List<MedigapFee>? Fees { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("has_brochure")]
    public bool? HasBrochure { get; set; }

    [JsonPropertyName("has_pdf_app")]
    public bool? HasPdfApp { get; set; }

    [JsonPropertyName("is_open_rate")]
    public bool? IsOpenRate { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("legacy_id")]
    public object? LegacyId { get; set; }

    [JsonPropertyName("plan")]
    public string? Plan { get; set; }

    [JsonPropertyName("rate")]
    public MedigapRate? Rate { get; set; }

    [JsonPropertyName("rate_increases")]
    public List<MedigapRateIncrease>? RateIncreases { get; set; }

    [JsonPropertyName("rate_type")]
    public string? RateType { get; set; }

    [JsonPropertyName("rating_class")]
    public string? RatingClass { get; set; }

    [JsonPropertyName("related_data")]
    public Dictionary<string, object>? RelatedData { get; set; }

    [JsonPropertyName("riders")]
    public List<object>? Riders { get; set; }

    [JsonPropertyName("select")]
    public bool? Select { get; set; }

    [JsonPropertyName("tobacco")]
    public bool? Tobacco { get; set; }

    [JsonPropertyName("view_type")]
    public List<string>? ViewType { get; set; }

    [JsonPropertyName("partBPremium")]
    public decimal? PartBPremium { get; set; }

    [JsonPropertyName("partBPremiumSurcharge")]
    public decimal? PartBPremiumSurcharge { get; set; }

    [JsonPropertyName("monthsUsedForExpenseCalc")]
    public int? MonthsUsedForExpenseCalc { get; set; }

    [JsonPropertyName("yearForPartBData")]
    public int? YearForPartBData { get; set; }

    [JsonPropertyName("medigapOOP")]
    public decimal? MedigapOOP { get; set; }

    [JsonPropertyName("partAServiceOOP")]
    public decimal? PartAServiceOOP { get; set; }

    [JsonPropertyName("partBServiceOOP")]
    public decimal? PartBServiceOOP { get; set; }

    [JsonPropertyName("naic")]
    public string? Naic { get; set; }
}

public class MedigapCompanyBase
{
    [JsonPropertyName("ambest_outlook")]
    public string? AmbestOutlook { get; set; }

    [JsonPropertyName("ambest_rating")]
    public string? AmbestRating { get; set; }

    [JsonPropertyName("business_type")]
    public string? BusinessType { get; set; }

    [JsonPropertyName("company_image_url")]
    public string? CompanyImageUrl { get; set; }

    [JsonPropertyName("customer_complaint_ratio")]
    public decimal? CustomerComplaintRatio { get; set; }

    [JsonPropertyName("customer_satisfaction_ratio")]
    public decimal? CustomerSatisfactionRatio { get; set; }

    [JsonPropertyName("established_year")]
    public int? EstablishedYear { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("med_supp_national_market_data")]
    public MedigapNationalMarketData? MedSuppNationalMarketData { get; set; }

    [JsonPropertyName("naic")]
    public string? Naic { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("name_full")]
    public string? NameFull { get; set; }

    [JsonPropertyName("parent_company_base")]
    public MedigapParentCompanyBase? ParentCompanyBase { get; set; }

    [JsonPropertyName("sp_rating")]
    public string? SpRating { get; set; }

    [JsonPropertyName("state_marketing_data")]
    public List<MedigapStateMarketingData>? StateMarketingData { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("underwriting_data")]
    public List<object>? UnderwritingData { get; set; }
}

public class MedigapParentCompanyBase
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("established_year")]
    public int? EstablishedYear { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class MedigapNationalMarketData
{
    [JsonPropertyName("claims")]
    public long? Claims { get; set; }

    [JsonPropertyName("lives")]
    public long? Lives { get; set; }

    [JsonPropertyName("market_share")]
    public decimal? MarketShare { get; set; }

    [JsonPropertyName("premiums")]
    public long? Premiums { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public class MedigapStateMarketingData
{
    [JsonPropertyName("marketing_name")]
    public string? MarketingName { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public class MedigapContextualData
{
    [JsonPropertyName("has_eapp")]
    public bool? HasEapp { get; set; }
}

public class MedigapDiscount
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("rule")]
    public object? Rule { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }
}

public class MedigapFee
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }
}

public class MedigapRate
{
    [JsonPropertyName("annual")]
    public decimal? Annual { get; set; }

    [JsonPropertyName("month")]
    public decimal? Month { get; set; }

    [JsonPropertyName("quarter")]
    public decimal? Quarter { get; set; }

    [JsonPropertyName("semi_annual")]
    public decimal? SemiAnnual { get; set; }
}

public class MedigapRateIncrease
{
    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("rate_increase")]
    public decimal? RateIncreaseValue { get; set; }
}