using Domain.Models;

namespace Domain.Interfaces;

public interface IRxNormService
{
    Task<RxNormResult?> NormalizeDrug(string drugName);
    Task<List<DrugInteraction>> GetInteractions(List<string> rxCuis);
    Task<List<string>> GetNdcsByRxCui(string rxCui);
}

public class RxNormResult
{
    public string RxCui { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}
