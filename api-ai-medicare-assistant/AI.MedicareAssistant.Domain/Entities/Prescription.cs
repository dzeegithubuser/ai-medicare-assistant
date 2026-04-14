using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class Prescription : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public User User { get; set; } = null!;

    public List<PrescriptionDrug> Drugs { get; set; } = [];
}

public class PrescriptionDrug : BaseEntity
{
    [Required]
    public Guid PrescriptionId { get; set; }

    [MaxLength(200)]
    public string DrugInput { get; set; } = "";

    [MaxLength(200)]
    public string NormalizedDrugName { get; set; } = "";

    [MaxLength(200)]
    public string GenericName { get; set; } = "";

    [MaxLength(200)]
    public string SelectedName { get; set; } = "";

    [MaxLength(20)]
    public string NameType { get; set; } = "";

    [MaxLength(100)]
    public string DosageForm { get; set; } = "";

    [MaxLength(100)]
    public string Strength { get; set; } = "";

    [MaxLength(200)]
    public string Packaging { get; set; } = "";

    [MaxLength(50)]
    public string RxNormId { get; set; } = "";

    [MaxLength(50)]
    public string NdcCode { get; set; } = "";

    [MaxLength(200)]
    public string TherapeuticCategory { get; set; } = "";

    [MaxLength(200)]
    public string DrugClass { get; set; } = "";

    public Prescription Prescription { get; set; } = null!;
}
