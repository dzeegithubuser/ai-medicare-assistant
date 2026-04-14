using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.Pipeline;

/// <summary>
/// Step 4: Fetches RxNorm drug-drug interactions and merges them
/// with interactions already identified by the AI.
/// </summary>
public class InteractionMergingStep : IDrugAnalysisStep
{
    private readonly IRxNormService _rxNorm;
    private readonly ILogger<InteractionMergingStep> _logger;

    public int Order => 4;

    public InteractionMergingStep(IRxNormService rxNorm, ILogger<InteractionMergingStep> logger)
    {
        _rxNorm = rxNorm;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(DrugAnalysisResult result, AnalysisContext context)
    {
        var rxCuis = result.Drugs
            .Where(d => !string.IsNullOrWhiteSpace(d.RxNormId))
            .Select(d => d.RxNormId)
            .Distinct()
            .ToList();

        if (rxCuis.Count < 2)
            return true;

        var rxNormInteractions = await _rxNorm.GetInteractions(rxCuis);
        if (rxNormInteractions.Count == 0)
            return true;

        var existingPairs = result.Interactions
            .Select(i => $"{i.DrugA.ToUpperInvariant()}|{i.DrugB.ToUpperInvariant()}")
            .ToHashSet();

        foreach (var rxInteraction in rxNormInteractions)
        {
            var pairKey = $"{rxInteraction.DrugA.ToUpperInvariant()}|{rxInteraction.DrugB.ToUpperInvariant()}";
            var reversePairKey = $"{rxInteraction.DrugB.ToUpperInvariant()}|{rxInteraction.DrugA.ToUpperInvariant()}";

            if (!existingPairs.Contains(pairKey) && !existingPairs.Contains(reversePairKey))
            {
                result.Interactions.Add(rxInteraction);
                existingPairs.Add(pairKey);
            }
        }

        _logger.LogInformation("Merged {Count} RxNorm interaction(s) with AI results",
            rxNormInteractions.Count);

        return true;
    }
}
