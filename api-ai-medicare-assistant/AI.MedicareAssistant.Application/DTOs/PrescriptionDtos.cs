using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

/// <summary>Upserts MongoDB "current" prescriptions and links the document id on the MySQL profile.</summary>
public class SaveCurrentPrescriptionsRequest
{
    [Required]
    public List<PrescriptionDrugDto> Drugs { get; set; } = [];

    /// <summary>Selected retail pharmacies from the FP pharmacy lookup step (up to 5).</summary>
    public List<SelectedPharmacySnapshotDto> SelectedPharmacies { get; set; } = [];

    /// <summary>Selected Part D / Medigap / MA plans from the FP plans step.</summary>
    public List<SelectedPlanSnapshotDto> SelectedPlans { get; set; } = [];

    /// <summary>FP UI section: partd | ma (optional).</summary>
    public string? FpActiveSection { get; set; }
}

/// <summary>Replaces only the drugs section in <c>userAnalysisSelections</c>. Does not touch pharmacies or plans.</summary>
public class SaveCurrentDrugsRequest
{
    [Required, MinLength(1)]
    public List<PrescriptionDrugDto> Drugs { get; set; } = [];
}

/// <summary>Replaces only the pharmacies section in <c>userAnalysisSelections</c>. Does not touch drugs or plans.</summary>
public class SaveCurrentPharmacyRequest
{
    [Required]
    public List<SelectedPharmacySnapshotDto> SelectedPharmacies { get; set; } = [];
}

/// <summary>Replaces only the plans section in <c>userAnalysisSelections</c>. Does not touch drugs or pharmacies.</summary>
public class SaveCurrentPlansRequest
{
    [Required]
    public List<SelectedPlanSnapshotDto> SelectedPlans { get; set; } = [];

    public string? FpActiveSection { get; set; }
}

public class SelectedPharmacySnapshotDto
{
    public string PharmacyNumber { get; set; } = "";
    public string PharmacyName { get; set; } = "";
    public string Address { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Zipcode { get; set; } = "";
}

/// <summary>One selected plan row for persistence (Part D, Medigap, MA, or MA gap Part D).</summary>
public class SelectedPlanSnapshotDto
{
    /// <summary>partD | medigap | ma | maGapPartD</summary>
    public string Slot { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string ContractId { get; set; } = "";
    public string? MedigapKey { get; set; }
    public string? MedigapPlanType { get; set; }
    public string? CompanyName { get; set; }
}

public class PrescriptionDrugDto
{
    public string DrugInput { get; set; } = "";
    public string NormalizedDrugName { get; set; } = "";
    public string GenericName { get; set; } = "";
    public string SelectedName { get; set; } = "";
    public string NameType { get; set; } = "";
    public string DosageForm { get; set; } = "";
    public string Strength { get; set; } = "";
    public string Packaging { get; set; } = "";
    public string RxNormId { get; set; } = "";
    public string NdcCode { get; set; } = "";
    public string TherapeuticCategory { get; set; } = "";
    public string DrugClass { get; set; } = "";

    public int? QuantityPerMonth { get; set; }
}

public class PrescriptionResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public List<PrescriptionDrugDto> Drugs { get; set; } = [];
    public List<SelectedPharmacySnapshotDto> SelectedPharmacies { get; set; } = [];
    public List<SelectedPlanSnapshotDto> SelectedPlans { get; set; } = [];
    /// <summary>From linked <c>userAnalysisSelections</c> document when available.</summary>
    public string? FpActiveSection { get; set; }
}
