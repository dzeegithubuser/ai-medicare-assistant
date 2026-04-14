using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class ProfileDto
{
    // Name fields
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(100)]
    public string LastName { get; set; } = "";

    // Coverage Year
    [Required]
    public int CoverageYear { get; set; }

    // Health Profile (1-5)
    [Required]
    public int HealthCondition { get; set; } = 1;

    // Tax Filing Status
    [Required, MaxLength(50)]
    public string TaxFilingStatus { get; set; } = "MARRIED_FILING_JOINTLY";

    // MAGI Tier
    [Required, MaxLength(50)]
    public string MagiTier { get; set; } = "";

    // Gender (M/F)
    [Required, MaxLength(10)]
    public string Gender { get; set; } = "F";

    // Tobacco Status (0=No, 1=Yes)
    [Required]
    public int TobaccoStatus { get; set; } = 0;

    // Date of Birth (yyyy-MM-dd)
    [Required]
    public string DateOfBirth { get; set; } = "";

    // Concierge (0=No, 1=Yes)
    [Required]
    public int Concierge { get; set; } = 0;

    // Concierge Amount (required when Concierge=1)
    public decimal? ConciergeAmount { get; set; }

    // Alternate Email
    [MaxLength(256), EmailAddress]
    public string? AlternateEmail { get; set; }

    // Alternate Mobile (US phone)
    [MaxLength(20), Phone]
    public string? AlternateMobile { get; set; }

    // Life Expectancy (65-120, default 95)
    [Required, Range(65, 120)]
    public int LifeExpectancy { get; set; } = 95;

    // ── Address fields ──

    [Required, MaxLength(256)]
    public string AddressLine1 { get; set; } = "";

    [Required, MaxLength(100)]
    public string City { get; set; } = "";

    [Required, MaxLength(50)]
    public string State { get; set; } = "";

    [Required, MaxLength(10)]
    public string ZipCode { get; set; } = "";

    [MaxLength(100)]
    public string County { get; set; } = "";

    [MaxLength(20)]
    public string CountyCode { get; set; } = "";

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

public class UserProfileResponse
{
    public ProfileDto? Profile { get; set; }
    public bool IsProfileComplete { get; set; }

    /// <summary>MongoDB id of the user's active "current" prescription list (from FP Drugs step).</summary>
    public string? CurrentPrescriptionDocumentId { get; set; }
}
