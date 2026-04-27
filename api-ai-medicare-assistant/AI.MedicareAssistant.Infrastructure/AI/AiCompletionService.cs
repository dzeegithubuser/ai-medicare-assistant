using Domain.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

/// <summary>
/// Default implementation of <see cref="IAiCompletionService"/> that delegates
/// to the configured <see cref="IChatClient"/> (Anthropic, Gemini, or OpenAI).
/// </summary>
public class AiCompletionService : IAiCompletionService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<AiCompletionService> _logger;

    public AiCompletionService(IChatClient chatClient, ILogger<AiCompletionService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        _logger.LogDebug("AI completion request — system prompt {SystemLen} chars, user prompt {UserLen} chars",
            systemPrompt.Length, userPrompt.Length);

        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var text = response.Text ?? string.Empty;

        _logger.LogDebug("AI completion response — {ResponseLen} chars", text.Length);

        return text;
    }
}
