using Application.Interfaces;
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
        services.AddScoped<IFinancialPlannerGroupRepository, MongoFinancialPlannerGroupRepository>();

        // ------- Core application services -------
        services.AddSingleton<IJwtTokenIssuer, Application.Services.JwtTokenIssuer>();
        services.AddScoped<IAuthService, Application.Services.AuthService>();
        services.AddScoped<IAdminService, Application.Services.AdminService>();
        services.AddScoped<IFinancialPlannerGroupService, Application.Services.FinancialPlannerGroupService>();
        services.AddScoped<IFinancialPlannerService, Application.Services.FinancialPlannerService>();
        services.AddScoped<IEndUserService, Application.Services.EndUserService>();
        services.AddScoped<IImpersonationService, Application.Services.ImpersonationService>();
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
