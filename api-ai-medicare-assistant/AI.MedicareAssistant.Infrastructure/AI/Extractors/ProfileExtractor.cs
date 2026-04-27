using Application.DTOs;
using Application.Interfaces;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI.Extractors;

public class ProfileExtractor : IProfileExtractor
{
    private readonly IAiCompletionService _aiService;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<ProfileExtractor> _logger;

    public ProfileExtractor(
        IAiCompletionService aiService,
        PromptBuilder promptBuilder,
        ILogger<ProfileExtractor> logger)
    {
        _aiService = aiService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<ProfileExtractResponse> ExtractAsync(ProfileExtractRequest request, CancellationToken cancellationToken = default)
    {
        var systemPrompt = _promptBuilder.LoadPromptFile("system/profile-extract-system.txt");
        var userMessage = $"Input message: \"{request.Message}\"\nMissing fields: [{string.Join(",", request.MissingFields.Select(f => $"\"{f}\""))}]";

        try
        {
            var raw = await _aiService.CompleteAsync(systemPrompt, userMessage, cancellationToken);
            var result = AiResponseParser.ParseJson<ProfileExtractResponse>(raw, _logger);
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
