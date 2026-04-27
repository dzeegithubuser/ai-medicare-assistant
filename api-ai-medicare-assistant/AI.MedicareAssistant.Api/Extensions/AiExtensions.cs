using Domain.Interfaces;
using Infrastructure.AI;
using Infrastructure.Anthropic;
using Infrastructure.Gemini;
using Microsoft.Extensions.AI;
using OpenAI;
using Serilog;

namespace Api.Extensions;

internal static class AiExtensions
{
    internal static IServiceCollection AddAiProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var aiProvider = configuration["AiProvider"] ?? "Anthropic";

        if (aiProvider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<AnthropicOptions>(configuration.GetSection("Anthropic"));

            services.AddHttpClient<IChatClient, AnthropicMeaiChatClient>((sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>()
                               .GetSection("Anthropic")
                               .Get<AnthropicOptions>();
                client.BaseAddress = new Uri(config?.BaseUrl ?? "https://api.anthropic.com/v1/");
            });

            Log.Information("AI provider: Anthropic ({Model})",
                configuration["Anthropic:Model"] ?? "claude-sonnet-4-20250514");
        }
        else if (aiProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));

            services.AddHttpClient<IChatClient, GeminiChatClient>((sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>()
                               .GetSection("Gemini")
                               .Get<GeminiOptions>();
                client.BaseAddress = new Uri(config?.BaseUrl ?? "https://generativelanguage.googleapis.com/v1beta/");
            });

            Log.Information("AI provider: Gemini ({Model})",
                configuration["Gemini:Model"] ?? "gemini-2.0-flash");
        }
        else
        {
            var openAiApiKey = configuration["OpenAI:ApiKey"]!;
            var openAiModel  = configuration["OpenAI:Model"] ?? "gpt-4o";

            var innerChatClient = new OpenAI.Chat.ChatClient(openAiModel, openAiApiKey).AsIChatClient();

            services.AddChatClient(innerChatClient).UseFunctionInvocation();

            Log.Information("AI provider: OpenAI ({Model})", openAiModel);
        }

        // ------- AI infrastructure -------
        services.AddSingleton<PromptBuilder>();
        services.AddScoped<IAiCompletionService, AiCompletionService>();
        services.AddScoped<IDrugAiService, DrugAiService>();
        services.AddScoped<IPlanScoringAiService, PlanScoringAiService>();
        services.AddScoped<ICostEvaluationAiService, CostEvaluationAiService>();
        services.AddScoped<ILtcEvaluationAiService, LtcEvaluationAiService>();
        services.AddScoped<IDrugInteractionAiService, DrugInteractionAiService>();

        // ------- AI extractors -------
        services.AddScoped<Application.Interfaces.IChatIntentClassifier, Infrastructure.AI.Extractors.ChatIntentClassifier>();
        services.AddScoped<Application.Interfaces.IProfileExtractor, Infrastructure.AI.Extractors.ProfileExtractor>();
        services.AddScoped<Application.Interfaces.IDrugSelectionExtractor, Infrastructure.AI.Extractors.DrugSelectionExtractor>();
        services.AddScoped<Application.Interfaces.IPharmacySelectionExtractor, Infrastructure.AI.Extractors.PharmacySelectionExtractor>();
        services.AddScoped<Application.Interfaces.IPlanSelectionExtractor, Infrastructure.AI.Extractors.PlanSelectionExtractor>();

        return services;
    }
}
