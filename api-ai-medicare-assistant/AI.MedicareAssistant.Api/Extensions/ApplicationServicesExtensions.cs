using Domain.Interfaces;
using Infrastructure.AI;
using Infrastructure.Repositories;

namespace Api.Extensions;

internal static class ApplicationServicesExtensions
{
    internal static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ------- SQL repositories -------
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();

        // ------- Core application services -------
        services.AddScoped<Application.Services.AuthService>();
        services.AddScoped<Application.Services.ProfileService>();
        services.AddScoped<Application.Services.PrescriptionService>();
        services.AddScoped<Application.Services.RecommendationService>();

        // ------- AI infrastructure services -------
        services.AddScoped<IPlanScoringAiService, PlanScoringAiService>();
        services.AddScoped<ICostEvaluationAiService, CostEvaluationAiService>();
        services.AddScoped<ILtcEvaluationAiService, LtcEvaluationAiService>();

        // ------- Cost & LTC application services -------
        services.AddScoped<Application.Services.CostProjectionService>();

        // ------- Chat services -------
        services.AddScoped<Application.Services.ChatIntentService>();
        services.AddScoped<Application.Services.ProfileExtractService>();
        services.AddScoped<Application.Services.DrugSelectionExtractService>();
        services.AddScoped<Application.Services.PharmacySelectionExtractService>();
        services.AddScoped<Application.Services.PlanSelectionExtractService>();
        services.AddScoped<Application.Services.ChatSessionService>();

        return services;
    }
}
