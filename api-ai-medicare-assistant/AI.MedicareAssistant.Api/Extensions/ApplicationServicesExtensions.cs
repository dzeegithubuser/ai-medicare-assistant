using Domain.Interfaces;
using Infrastructure.Repositories;

namespace Api.Extensions;

internal static class ApplicationServicesExtensions
{
    internal static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ------- MongoDB repositories -------
        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IProfileRepository, MongoProfileRepository>();

        // ------- Core application services -------
        services.AddScoped<Application.Services.AuthService>();
        services.AddScoped<Application.Services.ProfileService>();
        services.AddScoped<Application.Services.PrescriptionService>();
        services.AddScoped<Application.Services.RecommendationService>();

        // ------- Cost & LTC application services -------
        services.AddScoped<Application.Services.CostProjectionService>();

        // ------- Chat services -------
        services.AddScoped<Application.Services.ChatSessionService>();

        return services;
    }
}
