using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class SaveLtcCurrentRequest
{
    [Required, Range(1, 5)] public int HealthProfile { get; set; }
    [Required, Range(0, 20)] public int NumberOfAdultDayHealthCareYears { get; set; }
    [Required, Range(0, 20)] public int NumberOfHomeCareYears { get; set; }
    [Required, Range(0, 20)] public int NumberOfNursingCareYears { get; set; }
}

public class LtcCurrentResponse
{
    public int HealthProfile { get; set; }
    public int NumberOfAdultDayHealthCareYears { get; set; }
    public int NumberOfHomeCareYears { get; set; }
    public int NumberOfNursingCareYears { get; set; }
    public DateTime UpdatedAt { get; set; }
}
