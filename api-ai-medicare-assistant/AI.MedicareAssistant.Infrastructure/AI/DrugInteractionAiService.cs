using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class DrugInteractionAiService : IDrugInteractionAiService
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<DrugInteractionAiService> _logger;

    public DrugInteractionAiService(
        IAiCompletionService aiService,
        PromptBuilder promptBuilder,
        ILogger<DrugInteractionAiService> logger)
    {
        _aiService = aiService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<DrugInteractionAnalysis> EvaluateInteractionsAsync(
        List<string> drugNames,
        CancellationToken cancellationToken = default)
    {
        var drugList = string.Join("\n", drugNames.Select(d => $"- {d}"));

        var (systemPrompt, userPrompt) = _promptBuilder.Build("drug-interaction", new Dictionary<string, string>
        {
            ["{{DRUG_LIST}}"] = drugList
        });

        _logger.LogInformation("Evaluating drug interactions via AI for {Count} drugs", drugNames.Count);

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
            var analysis = AiResponseParser.ParseJson<DrugInteractionAnalysis>(raw, _logger);

            _logger.LogInformation(
                "AI interaction analysis complete: {Interactions} interactions, {Duplicates} duplicate therapies",
                analysis?.Interactions.Count ?? 0, analysis?.DuplicateTherapies.Count ?? 0);

            return analysis ?? new DrugInteractionAnalysis();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate drug interactions via AI");
            return new DrugInteractionAnalysis();
        }
    }
}
