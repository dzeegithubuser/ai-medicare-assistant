namespace Application.DTOs;

/// <summary>Derived display fields for a Part D plan card.</summary>
public class EnrichedPartDCard
{
    public string PlanIdDisplay { get; set; } = "";
    public string InsuranceCarrier { get; set; } = "";
    public double PartDSurcharge { get; set; }
    public double PrescriptionOOP { get; set; }
    public int PharmaciesInNetwork { get; set; }
    public int TotalSelectedPharmacies { get; set; }
    public int DrugsCovered { get; set; }
    public int TotalDrugs { get; set; }
}

/// <summary>Derived display fields for a Medigap plan card.</summary>
public class EnrichedMedigapCard
{
    public double PremiumMonthly { get; set; }
    public double PremiumAnnual { get; set; }
    public string InsuranceCarrier { get; set; } = "";
    public double PartBSurcharge { get; set; }
    public double HealthcareOOP { get; set; }
    public int RemainingMonths { get; set; }
}

/// <summary>Derived display fields for a Medicare Advantage plan card.</summary>
public class EnrichedMACard
{
    public string PlanIdDisplay { get; set; } = "";
    public string InsuranceCarrier { get; set; } = "";
    public double Surcharges { get; set; }
    public double PrescriptionOOP { get; set; }
    public double HealthcareOOP { get; set; }
    public bool HasPrescriptionDrug { get; set; }
    public int PharmaciesInNetwork { get; set; }
    public int TotalSelectedPharmacies { get; set; }
    public int DrugsCovered { get; set; }
    public int TotalDrugs { get; set; }
}
