using Application.DTOs;
using Application.Interfaces;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI.Extractors;

public class ChatIntentClassifier : IChatIntentClassifier
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<ChatIntentClassifier> _logger;

    public ChatIntentClassifier(
        IAiCompletionService aiService,
        PromptBuilder promptBuilder,
        ILogger<ChatIntentClassifier> logger)
    {
        _aiService = aiService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<ChatIntentResponse> ClassifyAsync(ChatIntentRequest request, CancellationToken cancellationToken = default)
    {
        var systemPrompt = _promptBuilder.LoadPromptFile("system/chat-intent-system.txt")
                         + PageContextBuilder.Build(request.CurrentPage);

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, request.Message, cancellationToken);
            var result = AiResponseParser.ParseJson<ChatIntentResponse>(raw, _logger);
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
