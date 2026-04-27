using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

/// <summary>
/// Centralizes the repeated AI-response post-processing pipeline:
/// strip markdown code fences → deserialize JSON → fallback on error.
/// </summary>
public static class AiResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Removes leading/trailing markdown code fences (```json … ```) that LLMs
    /// frequently wrap around JSON output.
    /// </summary>
    public static string StripMarkdownFences(string raw)
    {
        if (string.IsNullOrEmpty(raw) || !raw.StartsWith("```"))
            return raw;

        var firstNewline = raw.IndexOf('\n');
        var lastFence = raw.LastIndexOf("```");
        if (firstNewline >= 0 && lastFence > firstNewline)
            return raw[(firstNewline + 1)..lastFence].Trim();

        return raw;
    }

    /// <summary>
    /// Strips fences, deserializes JSON, and returns the result or null on failure.
    /// </summary>
    public static T? ParseJson<T>(string? raw, ILogger logger) where T : class
    {
        var clean = StripMarkdownFences(raw?.Trim() ?? "{}");

        try
        {
            return JsonSerializer.Deserialize<T>(clean, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize AI response to {Type}", typeof(T).Name);
            return null;
        }
    }

    /// <summary>
    /// Strips fences, deserializes JSON, and returns the result or a fallback on failure.
    /// </summary>
    public static T ParseJsonWithFallback<T>(string? raw, T fallback, ILogger logger) where T : class
    {
        return ParseJson<T>(raw, logger) ?? fallback;
    }
}
