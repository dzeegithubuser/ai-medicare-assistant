namespace Domain.Interfaces;

/// <summary>
/// Abstracts AI chat-completion calls behind a simple prompt-in / text-out contract.
/// Implementations wrap the configured LLM provider (Anthropic, Gemini, OpenAI)
/// and provide a single seam for logging, retry, and token tracking.
/// </summary>
public interface IAiCompletionService
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
