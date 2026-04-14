using Domain.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class DrugAiService : IDrugAiService
{
    private readonly IChatClient _client;
    private readonly PromptBuilder _builder;
    private readonly ILogger<DrugAiService> _logger;

    public DrugAiService(IChatClient client, PromptBuilder builder, ILogger<DrugAiService> logger)
    {
        _client = client;
        _builder = builder;
        _logger = logger;
    }

    public async Task<string> AnalyzePrescription(string prescription)
    {
        _logger.LogInformation("Sending prescription to AI provider (length={Length})", prescription.Length);

        var prompts=_builder.Build(prescription);

        var messages=new List<ChatMessage>
        {
            new(ChatRole.System,prompts.system),
            new(ChatRole.User,prompts.user)
        };

        var response=await _client.GetResponseAsync(messages);

        _logger.LogInformation("AI response received ({Length} chars)", response.Text.Length);

        return response.Text;
    }

    public async Task<string> SuggestDrugNames(string input)
    {
        _logger.LogInformation("Requesting drug name suggestions from AI provider (length={Length})", input.Length);

        var prompts = _builder.BuildDrugNameSuggestion(input);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompts.system),
            new(ChatRole.User, prompts.user)
        };

        var response = await _client.GetResponseAsync(messages);

        _logger.LogInformation("Drug name suggestions received ({Length} chars)", response.Text.Length);

        return response.Text;
    }
}
