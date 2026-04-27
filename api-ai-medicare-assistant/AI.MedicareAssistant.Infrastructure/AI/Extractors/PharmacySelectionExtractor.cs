using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI.Extractors;

public class PharmacySelectionExtractor : IPharmacySelectionExtractor
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<PharmacySelectionExtractor> _logger;

    public PharmacySelectionExtractor(
        IAiCompletionService aiService,
        PromptBuilder promptBuilder,
        ILogger<PharmacySelectionExtractor> logger)
    {
        _aiService = aiService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<PharmacySelectionExtractResponse> ExtractAsync(PharmacySelectionExtractRequest request, CancellationToken cancellationToken = default)
    {
        var systemPrompt = _promptBuilder.LoadPromptFile("system/pharmacy-selection-system.txt");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var availableJson = JsonSerializer.Serialize(request.AvailablePharmacies, options);
        var selectedJson = JsonSerializer.Serialize(request.SelectedPharmacies, options);
        var userMessage = $"Input message: \"{request.Message}\"\nAvailable pharmacies: {availableJson}\nSelected pharmacies: {selectedJson}";

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, userMessage, cancellationToken);
            var result = AiResponseParser.ParseJson<PharmacySelectionExtractResponse>(raw, _logger);
            return result ?? FallbackResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pharmacy selection extraction failed for message: {Message}", request.Message);
            return FallbackResponse();
        }
    }

    private static PharmacySelectionExtractResponse FallbackResponse() => new()
    {
        Action = "select",
        Reply = "I couldn't understand that. Try something like: \"select CVS\" or \"remove Walgreens\" or \"which pharmacies did I select?\""
    };
}
