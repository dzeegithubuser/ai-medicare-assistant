using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI.Extractors;

public class PlanSelectionExtractor : IPlanSelectionExtractor
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<PlanSelectionExtractor> _logger;

    public PlanSelectionExtractor(
        IAiCompletionService aiService,
        PromptBuilder promptBuilder,
        ILogger<PlanSelectionExtractor> logger)
    {
        _aiService = aiService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<PlanSelectionExtractResponse> ExtractAsync(PlanSelectionExtractRequest request, CancellationToken cancellationToken = default)
    {
        var systemPrompt = _promptBuilder.LoadPromptFile("system/plan-selection-system.txt");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var partDJson = JsonSerializer.Serialize(request.AvailablePartDPlans, options);
        var medigapJson = JsonSerializer.Serialize(request.AvailableMedigapPlans, options);
        var maJson = JsonSerializer.Serialize(request.AvailableMAPlans, options);
        var selectedJson = JsonSerializer.Serialize(request.SelectedPlans, options);

        var userMessage = $"""
            Input message: "{request.Message}"
            Active section: {request.ActiveSection ?? "none"}
            Available Part D plans: {partDJson}
            Available Medigap plans: {medigapJson}
            Available MA plans: {maJson}
            Selected plans: {selectedJson}
            """;

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, userMessage, cancellationToken);
            var result = AiResponseParser.ParseJson<PlanSelectionExtractResponse>(raw, _logger);
            return result ?? FallbackResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plan selection extraction failed for message: {Message}", request.Message);
            return FallbackResponse();
        }
    }

    private static PlanSelectionExtractResponse FallbackResponse() => new()
    {
        Action = "list",
        Reply = "I couldn't understand that. Try something like: \"select the Humana Part D plan\" or \"remove the medigap plan\" or \"what plans are available?\""
    };
}
