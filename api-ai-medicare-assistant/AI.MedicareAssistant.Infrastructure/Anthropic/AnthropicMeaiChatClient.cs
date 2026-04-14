using Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Anthropic;

/// <summary>
/// Anthropic implementation of M.E.AI <see cref="IChatClient"/>,
/// allowing all services to use a single AI abstraction regardless of provider.
/// </summary>
public class AnthropicMeaiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicMeaiChatClient> _logger;

    public AnthropicMeaiChatClient(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicMeaiChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public ChatClientMetadata Metadata => new("anthropic", new Uri(_options.BaseUrl), _options.Model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messagesList = chatMessages.ToList();

        var systemPrompt = string.Join("\n",
            messagesList
                .Where(m => m.Role == ChatRole.System)
                .SelectMany(m => m.Contents.OfType<TextContent>())
                .Select(c => c.Text));

        var anthropicMessages = messagesList
            .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
            .Select(m => new
            {
                role = m.Role == ChatRole.User ? "user" : "assistant",
                content = string.Join("\n",
                    m.Contents.OfType<TextContent>().Select(c => c.Text))
            })
            .ToList();

        var requestBody = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            messages = anthropicMessages
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        _logger.LogInformation("Sending request to Anthropic model {Model}", _options.Model);

        var request = new HttpRequestMessage(HttpMethod.Post, "messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API error: {StatusCode} - {Response}", response.StatusCode, responseText);
            throw new InvalidOperationException($"Anthropic API error: {response.StatusCode} - {responseText}");
        }

        using var doc = JsonDocument.Parse(responseText);
        var contentArray = doc.RootElement.GetProperty("content");

        var text = string.Join("\n",
            contentArray.EnumerateArray()
                .Where(x => x.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(x => x.GetProperty("text").GetString() ?? string.Empty));

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    /// <summary>
    /// NOTE: True SSE-based streaming is not implemented for the Anthropic provider.
    /// This method fetches the full response and yields it as a single update to satisfy
    /// the <see cref="IChatClient"/> contract. Callers should prefer
    /// <see cref="GetResponseAsync"/> when real-time token streaming is not required.
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(chatMessages, options, cancellationToken);
        var text = response.Text ?? string.Empty;

        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent(text)]
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(AnthropicMeaiChatClient) ? this : null;

    public void Dispose() { }
}
