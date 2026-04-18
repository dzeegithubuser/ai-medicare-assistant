using Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Gemini;

/// <summary>
/// Google Gemini implementation of M.E.AI <see cref="IChatClient"/>,
/// allowing all services to use a single AI abstraction regardless of provider.
/// </summary>
public class GeminiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiChatClient> _logger;

    public GeminiChatClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public ChatClientMetadata Metadata => new("gemini", new Uri(_options.BaseUrl), _options.Model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messagesList = chatMessages.ToList();

        // Gemini uses a top-level system_instruction field for system prompts
        var systemText = string.Join("\n",
            messagesList
                .Where(m => m.Role == ChatRole.System)
                .SelectMany(m => m.Contents.OfType<TextContent>())
                .Select(c => c.Text));

        // Gemini roles: "user" and "model" (not "assistant")
        var contents = messagesList
            .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
            .Select(m => new
            {
                role = m.Role == ChatRole.User ? "user" : "model",
                parts = m.Contents.OfType<TextContent>()
                    .Select(c => new { text = c.Text })
                    .ToArray()
            })
            .ToArray();

        var requestBody = BuildRequestBody(systemText, contents);

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        _logger.LogInformation("Sending request to Gemini model {Model}", _options.Model);

        // API key passed as query parameter per Gemini REST API spec
        var endpoint = $"models/{_options.Model}:generateContent?key={_options.ApiKey}";
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API error: {StatusCode} - {Response}", response.StatusCode, responseText);
            throw new InvalidOperationException($"Gemini API error: {response.StatusCode} - {responseText}");
        }

        var text = ParseGeminiResponse(responseText);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    /// <summary>
    /// NOTE: True SSE-based streaming is not implemented for the Gemini provider.
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
        => serviceType == typeof(GeminiChatClient) ? this : null;

    public void Dispose() { }

    // ── Private helpers ──────────────────────────────────────────────────

    private object BuildRequestBody(string systemText, object contents)
    {
        var generationConfig = new
        {
            maxOutputTokens = _options.MaxOutputTokens
        };

        if (!string.IsNullOrWhiteSpace(systemText))
        {
            return new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemText } }
                },
                contents,
                generationConfig
            };
        }

        return new { contents, generationConfig };
    }

    private static string ParseGeminiResponse(string responseText)
    {
        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;

        // candidates[0].content.parts[0].text
        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textProp))
            {
                return textProp.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
