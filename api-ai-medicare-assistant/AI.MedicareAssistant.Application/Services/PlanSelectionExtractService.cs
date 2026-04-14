using System.Text.Json;
using Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class PlanSelectionExtractService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<PlanSelectionExtractService> _logger;
    private readonly string _systemPrompt;

    private static readonly string PromptPath = Path.Combine("Prompts", "system", "plan-selection-system.txt");

    public PlanSelectionExtractService(IChatClient chatClient, ILogger<PlanSelectionExtractService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _systemPrompt = File.ReadAllText(PromptPath);
    }

    public async Task<PlanSelectionExtractResponse> ExtractAsync(PlanSelectionExtractRequest request, CancellationToken ct = default)
    {
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

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, _systemPrompt),
            new(ChatRole.User, userMessage)
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var json = (response.Text ?? string.Empty).Trim();

            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```");
                if (firstNewline >= 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            var result = JsonSerializer.Deserialize<PlanSelectionExtractResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
