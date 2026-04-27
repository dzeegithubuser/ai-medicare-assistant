using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI.Extractors;

public class DrugSelectionExtractor : IDrugSelectionExtractor
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<DrugSelectionExtractor> _logger;

    public DrugSelectionExtractor(
        IAiCompletionService aiService,
        PromptBuilder promptBuilder,
        ILogger<DrugSelectionExtractor> logger)
    {
        _aiService = aiService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<DrugSelectionExtractResponse> ExtractAsync(DrugSelectionExtractRequest request, CancellationToken cancellationToken = default)
    {
        var systemPrompt = _promptBuilder.LoadPromptFile("system/drug-selection-system.txt");
        var drugsJson = JsonSerializer.Serialize(request.AvailableDrugs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var userMessage = $"Input message: \"{request.Message}\"\nAvailable drugs: {drugsJson}";

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, userMessage, cancellationToken);
            var result = AiResponseParser.ParseJson<DrugSelectionExtractResponse>(raw, _logger);
            return result ?? FallbackResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drug selection extraction failed for message: {Message}", request.Message);
            return FallbackResponse();
        }
    }

    private static DrugSelectionExtractResponse FallbackResponse() => new()
    {
        Action = "select",
        Reply = "I couldn't understand that. Try something like: \"select Lisinopril generic tablet 10mg 30 per month\" or \"what options are available for Metformin?\""
    };
}
