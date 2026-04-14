using System.Text.Json;
using Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class ProfileExtractService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ProfileExtractService> _logger;
    private readonly string _systemPrompt;

    private static readonly string PromptPath = Path.Combine("Prompts", "system", "profile-extract-system.txt");

    public ProfileExtractService(IChatClient chatClient, ILogger<ProfileExtractService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
        _systemPrompt = File.ReadAllText(PromptPath);
    }

    public async Task<ProfileExtractResponse> ExtractAsync(ProfileExtractRequest request, CancellationToken ct = default)
    {
        var userMessage = $"Input message: \"{request.Message}\"\nMissing fields: [{string.Join(",", request.MissingFields.Select(f => $"\"{f}\""))}]";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, _systemPrompt),
            new(ChatRole.User, userMessage)
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

            var result = JsonSerializer.Deserialize<ProfileExtractResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? FallbackResponse(request.MissingFields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile extraction failed for message: {Message}", request.Message);
            return FallbackResponse(request.MissingFields);
        }
    }

    private static ProfileExtractResponse FallbackResponse(List<string> missingFields) => new()
    {
        ExtractedFields = new Dictionary<string, object>(),
        Reply = missingFields.Count > 0
            ? $"I couldn't extract profile data from that. I still need: {string.Join(", ", missingFields.Select(FormatFieldName))}. Try again with your details!"
            : "I couldn't extract profile data from that. Please try again with your information."
    };

    private static string FormatFieldName(string field) => field switch
    {
        "firstName" => "first name",
        "lastName" => "last name",
        "dateOfBirth" => "date of birth",
        "gender" => "gender",
        "tobaccoStatus" => "tobacco status",
        "healthCondition" => "health condition (1-5)",
        "taxFilingStatus" => "tax filing status",
        "coverageYear" => "coverage year",
        "magiTier" => "MAGI tier",
        "zipCode" => "ZIP code",
        "addressLine1" => "street address",
        "lifeExpectancy" => "life expectancy",
        "concierge" => "concierge service",
        "conciergeAmount" => "concierge amount",
        "alternateEmail" => "alternate email",
        "alternateMobile" => "alternate mobile",
        _ => field
    };
}
