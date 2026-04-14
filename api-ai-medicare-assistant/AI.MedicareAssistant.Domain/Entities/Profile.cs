using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class Profile : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    // Name fields
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(100)]
    public string LastName { get; set; } = "";

    // Coverage Year
    public int CoverageYear { get; set; }

    // Health Profile (1=Best, 2=Good, 3=Moderate, 4=Poor, 5=Sick)
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
    public int TobaccoStatus { get; set; } = 0;

    // Date of Birth (must be 18+)
    public DateOnly DateOfBirth { get; set; }

    // Concierge (0=No, 1=Yes)
    public int Concierge { get; set; } = 0;

    // Concierge Amount (optional when Concierge=0, required when Concierge=1)
    public decimal? ConciergeAmount { get; set; }

    // Alternate Email
    [MaxLength(256)]
    public string? AlternateEmail { get; set; }

    // Alternate Mobile (US phone)
    [MaxLength(20)]
    public string? AlternateMobile { get; set; }

    // Life Expectancy (65-120, default 95)
    public int LifeExpectancy { get; set; } = 95;

    // ── Address fields ──

    [MaxLength(256)]
    public string AddressLine1 { get; set; } = "";

    [MaxLength(100)]
    public string City { get; set; } = "";

    [MaxLength(50)]
    public string State { get; set; } = "";

    [MaxLength(10)]
    public string ZipCode { get; set; } = "";

    [MaxLength(100)]
    public string County { get; set; } = "";

    [MaxLength(20)]
    public string CountyCode { get; set; } = "";

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>MongoDB prescription document Id for the user's current analysis drugs (updated when leaving the Drugs step).</summary>
    [MaxLength(24)]
    public string? CurrentPrescriptionDocumentId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
