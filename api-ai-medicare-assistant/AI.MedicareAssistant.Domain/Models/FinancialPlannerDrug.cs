namespace Domain.Models;

public class DrugSearchRequest
{
    public required string DrugName { get; set; }
}

public class DrugSearchResponse
{
    public string WebServiceTransactionId { get; set; } = "";
    public string WebServiceStatus { get; set; } = "";
    public string DrugName { get; set; } = "";
    public List<DrugListItem> DrugList { get; set; } = [];
    public List<object> Messages { get; set; } = [];
}

public class DrugListItem
{
    public string Rxcui { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Prescription { get; set; }
}

public class DrugDetailRequest
{
    public required string Rxcui { get; set; }
}

public class DrugDetailResponse
{
    public string WebServiceTransactionId { get; set; } = "";
    public string WebServiceStatus { get; set; } = "";
    public string Rxcui { get; set; } = "";
    public List<DrugDetailAdvanceItem> DrugDetailAdvanceList { get; set; } = [];
}

public class DrugDetailAdvanceItem
{
    public string DrugName { get; set; } = "";
    public string Rxcui { get; set; } = "";
    public string GenericDrugName { get; set; } = "";
    public string GenericRxcui { get; set; } = "";
    public string NewDoseForm { get; set; } = "";
    public string RxnDoseForm { get; set; } = "";
    public string Strength { get; set; } = "";
    public string BrandName { get; set; } = "";
    public bool Prescription { get; set; }
    public string DrugType { get; set; } = "";
}

public class DrugSearchResult
{
    public string DrugName { get; set; } = "";
    public DrugSearchResponse Search { get; set; } = new();
    public DrugListItem? MatchedDrug { get; set; }
    public DrugDetailResponse? Detail { get; set; }
}

public class BulkDrugSearchResponse
{
    public List<DrugSearchResult> Results { get; set; } = [];
    public List<DrugInteraction> Interactions { get; set; } = [];
    public List<DuplicateTherapy> DuplicateTherapies { get; set; } = [];
}

public class DrugInteractionAnalysis
{
    public List<DrugInteraction> Interactions { get; set; } = [];
    public List<DuplicateTherapy> DuplicateTherapies { get; set; } = [];
}
