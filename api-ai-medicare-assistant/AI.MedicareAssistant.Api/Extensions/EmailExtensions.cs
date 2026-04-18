using Domain.Interfaces;
using Infrastructure.Email;

namespace Api.Extensions;

internal static class EmailExtensions
{
    internal static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.AddScoped<IEmailService, EmailService>();
        return services;
    }
}
