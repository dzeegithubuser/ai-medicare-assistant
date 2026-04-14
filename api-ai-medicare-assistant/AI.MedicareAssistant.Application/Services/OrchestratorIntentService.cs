using Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.Services;

/// <summary>
/// Classifies user messages into one of 19 domain intents for the chatbot orchestrator.
/// Uses the same IChatClient as ChatIntentService but with a domain-specific system prompt.
/// </summary>
public class OrchestratorIntentService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<OrchestratorIntentService> _logger;
    private readonly string _systemPrompt;

    private static readonly string PromptPath =
        Path.Combine("Prompts", "system", "orchestrator-intent-system.txt");

    public OrchestratorIntentService(IChatClient chatClient, ILogger<OrchestratorIntentService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _systemPrompt = File.ReadAllText(PromptPath);
    }

    public async Task<OrchestratorIntentResult> ClassifyAsync(string message, string? currentPage = null, CancellationToken ct = default)
    {
        var effectiveSystemPrompt = _systemPrompt + PageContextBuilder.Build(currentPage);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, effectiveSystemPrompt),
            new(ChatRole.User, message)
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var json = (response.Text ?? string.Empty).Trim();

            // Strip markdown fences if present
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```");
                if (firstNewline >= 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            var result = JsonSerializer.Deserialize<OrchestratorIntentResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result is not null)
            {
                _logger.LogInformation("Orchestrator intent: {Intent} for message: {Message}", result.Intent, message);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestrator intent classification failed for: {Message}", message);
        }

        return new OrchestratorIntentResult { Intent = "unknown" };
    }
}
