using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class LongTermCareRequest
{
    [Required] public int Age { get; set; }
    [Required] public int PvAsOfYear { get; set; }
    [Required] public int LifeExpectancy { get; set; }
    public string TransactionTypeFlag { get; set; } = "false";
    [Required] public int HealthProfile { get; set; }
    [Required] public string Location { get; set; } = "";
    [Required] public string Zipcode { get; set; } = "";
    public int Tobacco { get; set; }
    public int CurrentLifeStyleExpenses { get; set; } = 1;
    public int NumberOfAdultDayHealthCareLTCYears { get; set; }
    public int NumberOfAssistedCareLTCYears { get; set; } = 0;
    public int NumberOfHomeCareLTCYears { get; set; }
    public int NumberOfNursingCareLTCYears { get; set; }
    [Required] public string Gender { get; set; } = "";
    public int AlzheimersFlag { get; set; } = 0;
    public int HeartStorkeFlag { get; set; } = 0;
}

public class LtcExpenseEntry
{
    public int Year { get; set; }
    public decimal Expense { get; set; }
}

public class LongTermCareResponse
{
    public string? WebServiceTransactionId { get; set; }
    public string? WebServiceStatus { get; set; }
    public bool TransactionTypeFlag { get; set; }
    public int Age { get; set; }
    public int HealthProfile { get; set; }
    public string? Gender { get; set; }
    public string? State { get; set; }
    public string? Region { get; set; }
    public int Zipcode { get; set; }
    public int CountyCode { get; set; }
    public int LifeExpenctancy { get; set; }
    public bool TobaccoUsage { get; set; }
    public bool AlzheimersFlag { get; set; }
    public bool HeartStorkeFlag { get; set; }
    public int CurrentLifeStyleExpenses { get; set; }
    public int NumberOfAdultDayHealthCareLTCYears { get; set; }
    public int NumberOfHomeCareLTCYears { get; set; }
    public int NumberOfAssistedCareLTCYears { get; set; }
    public int NumberOfNursingCareLTCYears { get; set; }
    public int StartingYearOfAdultDayHealthCare { get; set; }
    public int StartingYearOfHomeCare { get; set; }
    public int StartingYearOfAssistedCare { get; set; }
    public int StartingYearOfNursingCare { get; set; }
    public decimal AdultDayHealthCare { get; set; }
    public decimal PresentValueAdultDayHealthCare { get; set; }
    public decimal HomeCare { get; set; }
    public decimal PresentValueHomeCare { get; set; }
    public decimal AssistedCare { get; set; }
    public decimal PresentValueAssistedCare { get; set; }
    public decimal NursingCare { get; set; }
    public decimal PresentValueNursingCare { get; set; }
    public List<LtcExpenseEntry> FutureAdultDayHealthCareExpenseList { get; set; } = [];
    public decimal ExpectedAdultDayHealthCare { get; set; }
    public decimal PresentValueExpectedAdultDayHealthCare { get; set; }
    public List<LtcExpenseEntry> FutureHomeCareExpenseList { get; set; } = [];
    public decimal ExpectedHomeCare { get; set; }
    public decimal PresentValueExpectedHomeCare { get; set; }
    public List<LtcExpenseEntry> FutureAssistedCareExpensesList { get; set; } = [];
    public decimal ExpectedAssistedCare { get; set; }
    public decimal PresentValueExpectedAssistedCare { get; set; }
    public List<LtcExpenseEntry> FutureNursingCareExpensesList { get; set; } = [];
    public decimal ExpectedNursingCare { get; set; }
    public decimal PresentValueExpectedNursingCare { get; set; }
    public int PresentValueYear { get; set; }
}
