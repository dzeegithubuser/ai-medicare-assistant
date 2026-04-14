using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Domain.Models;

public class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1/";
    public int MaxTokens { get; set; } = 2000;
}

public class AnthropicMessageRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1000;

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = new();
}

public class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class AnthropicResponse
{
    [JsonPropertyName("content")]
    public List<AnthropicContentBlock>? Content { get; set; }
}

public class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
