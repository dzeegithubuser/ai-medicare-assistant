using Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.Services;

public class ChatIntentService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ChatIntentService> _logger;
    private readonly string _systemPrompt;

    private static readonly string PromptPath = Path.Combine("Prompts", "system", "chat-intent-system.txt");

    public ChatIntentService(IChatClient chatClient, ILogger<ChatIntentService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _systemPrompt = File.ReadAllText(PromptPath);
    }

    public async Task<ChatIntentResponse> ClassifyAsync(ChatIntentRequest request, CancellationToken ct = default)
    {
        var effectiveSystemPrompt = _systemPrompt + PageContextBuilder.Build(request.CurrentPage);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, effectiveSystemPrompt),
            new(ChatRole.User, request.Message)
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var json = (response.Text ?? string.Empty).Trim();

            // Strip markdown fences if present (defensive)
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```");
                if (firstNewline >= 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            var result = JsonSerializer.Deserialize<ChatIntentResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? FallbackResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Intent classification failed for message: {Message}", request.Message);
            return FallbackResponse();
        }
    }

    private static ChatIntentResponse FallbackResponse() => new()
    {
        Intent = "UNKNOWN",
        ConfirmationMessage = "I'm not sure what you'd like to do. Try entering drug names or use the menu."
    };

}
