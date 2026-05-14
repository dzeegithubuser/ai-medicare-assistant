using Api.Filters;

namespace Api.Extensions;

internal static class CoreServicesExtensions
{
    internal static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<MustChangePasswordFilter>();
        });
        services.AddSignalR();
        services.AddMemoryCache();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                              ?? ["http://localhost:4200", "http://169.61.105.110:9600"];
                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
