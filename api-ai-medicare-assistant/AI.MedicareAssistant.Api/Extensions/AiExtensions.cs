using Domain.Interfaces;
using Domain.Models;
using Infrastructure.AI;
using Infrastructure.Anthropic;
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
        services.AddScoped<IDrugAiService, DrugAiService>();

        return services;
    }
}
