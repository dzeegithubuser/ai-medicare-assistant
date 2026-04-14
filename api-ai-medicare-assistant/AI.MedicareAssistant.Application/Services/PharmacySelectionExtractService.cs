using System.Text.Json;
using Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class PharmacySelectionExtractService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<PharmacySelectionExtractService> _logger;
    private readonly string _systemPrompt;

    private static readonly string PromptPath = Path.Combine("Prompts", "system", "pharmacy-selection-system.txt");

    public PharmacySelectionExtractService(IChatClient chatClient, ILogger<PharmacySelectionExtractService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _systemPrompt = File.ReadAllText(PromptPath);
    }

    public async Task<PharmacySelectionExtractResponse> ExtractAsync(PharmacySelectionExtractRequest request, CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var availableJson = JsonSerializer.Serialize(request.AvailablePharmacies, options);
        var selectedJson = JsonSerializer.Serialize(request.SelectedPharmacies, options);

        var userMessage = $"Input message: \"{request.Message}\"\nAvailable pharmacies: {availableJson}\nSelected pharmacies: {selectedJson}";

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

            var result = JsonSerializer.Deserialize<PharmacySelectionExtractResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
