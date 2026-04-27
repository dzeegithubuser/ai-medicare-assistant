using Domain.Models;

namespace Domain.Interfaces;

public interface IDrugInteractionAiService
{
    Task<DrugInteractionAnalysis> EvaluateInteractionsAsync(
        List<string> drugNames,
        CancellationToken cancellationToken = default);
}
