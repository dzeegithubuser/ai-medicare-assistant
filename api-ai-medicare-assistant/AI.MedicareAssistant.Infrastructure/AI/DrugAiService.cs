using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class DrugAiService : IDrugAiService
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _builder;
    private readonly ILogger<DrugAiService> _logger;

    public DrugAiService(IAiCompletionService aiService, PromptBuilder builder, ILogger<DrugAiService> logger)
    {
        _aiService = aiService;
        _builder = builder;
        _logger = logger;
    }

    public async Task<string> AnalyzePrescription(string prescription)
    {
        _logger.LogInformation("Sending prescription to AI provider (length={Length})", prescription.Length);

        try
        {
            var (system, user) = _builder.BuildDrugNormalization(prescription);
            var response = await _aiService.CompleteAsync(system, user);

            _logger.LogInformation("AI response received ({Length} chars)", response.Length);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prescription analysis AI call failed");
            return "{}";
        }
    }

    public async Task<string> SuggestDrugNames(string input)
    {
        _logger.LogInformation("Requesting drug name suggestions from AI provider (length={Length})", input.Length);

        try
        {
            var (system, user) = _builder.Build("drug-name-suggestion", new Dictionary<string, string>
            {
                ["{{INPUT}}"] = input
            });

            var response = await _aiService.CompleteAsync(system, user);

            _logger.LogInformation("Drug name suggestions received ({Length} chars)", response.Length);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drug name suggestion AI call failed for input: {Input}", input);
            return "{}";
        }
    }
}
