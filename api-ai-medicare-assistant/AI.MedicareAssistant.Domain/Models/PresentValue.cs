using System.Text.Json.Serialization;

namespace Domain.Models;

public class PresentValueRequest
{
    [JsonPropertyName("fromYear")]
    public int FromYear { get; set; }

    [JsonPropertyName("toYear")]
    public int ToYear { get; set; }

    [JsonPropertyName("expenses")]
    public List<YearExpense> Expenses { get; set; } = [];

    [JsonPropertyName("presentValueYears")]
    public PresentValueYears PresentValueYears { get; set; } = new();

    [JsonPropertyName("discount")]
    public int Discount { get; set; } = 6;

    [JsonPropertyName("rateOfReturns")]
    public RateOfReturns RateOfReturns { get; set; } = new();
}

public class YearExpense
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("expense")]
    public decimal Expense { get; set; }
}

public class PresentValueYears
{
    [JsonPropertyName("pvAsOnYear1")]
    public int PvAsOnYear1 { get; set; }
}

public class RateOfReturns
{
    [JsonPropertyName("rateOfReturn1")]
    public int RateOfReturn1 { get; set; }
}

public class PresentValueResponse
{
    [JsonPropertyName("pvList")]
    public List<PvEntry> PvList { get; set; } = [];

    [JsonPropertyName("webServiceStatus")]
    public string WebServiceStatus { get; set; } = "";
}

public class PvEntry
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("totalExpense")]
    public decimal TotalExpense { get; set; }

    [JsonPropertyName("presentValue")]
    public decimal PresentValue { get; set; }
}
