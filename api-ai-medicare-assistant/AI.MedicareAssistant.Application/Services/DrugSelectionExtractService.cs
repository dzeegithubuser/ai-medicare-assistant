using System.Text.Json;
using Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class DrugSelectionExtractService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<DrugSelectionExtractService> _logger;
    private readonly string _systemPrompt;

    private static readonly string PromptPath = Path.Combine("Prompts", "system", "drug-selection-system.txt");

    public DrugSelectionExtractService(IChatClient chatClient, ILogger<DrugSelectionExtractService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _systemPrompt = File.ReadAllText(PromptPath);
    }

    public async Task<DrugSelectionExtractResponse> ExtractAsync(DrugSelectionExtractRequest request, CancellationToken ct = default)
    {
        var drugsJson = JsonSerializer.Serialize(request.AvailableDrugs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var userMessage = $"Input message: \"{request.Message}\"\nAvailable drugs: {drugsJson}";

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

            var result = JsonSerializer.Deserialize<DrugSelectionExtractResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
