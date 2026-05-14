using Domain.Constants;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

/// <summary>
/// Seeds the singleton admin user (<c>admin@aivante.com</c>) on startup if the
/// <c>Seed:AdminPassword</c> configuration value is set. The admin user is created
/// with <c>MustChangePassword=true</c> so the password must be reset on first sign-in.
/// </summary>
public class AdminSeedInitializer : IHostedService
{
    /// <summary>Default admin email if <c>Seed:AdminEmail</c> is not configured.</summary>
    public const string DefaultAdminEmail = "admin@aivante.com";

    /// <summary>Default admin phone if <c>Seed:AdminPhone</c> is not configured.</summary>
    public const string DefaultAdminPhone = "5550199999";

    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeedInitializer> _logger;

    public AdminSeedInitializer(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<AdminSeedInitializer> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seedPassword = _configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            _logger.LogInformation(
                "Admin seed skipped: Seed:AdminPassword is not configured.");
            return;
        }

        var adminEmail = (_configuration["Seed:AdminEmail"] ?? DefaultAdminEmail).Trim().ToLowerInvariant();
        var adminPhone = (_configuration["Seed:AdminPhone"] ?? DefaultAdminPhone).Trim();

        using var scope = _services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        if (await userRepo.EmailExistsAsync(adminEmail))
        {
            _logger.LogInformation("Admin user {Email} already exists; skipping seed.", adminEmail);
            return;
        }

        var admin = new UserDocument
        {
            Email = adminEmail,
            Phone = adminPhone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
            FirstName = "Aivante",
            LastName = "Admin",
            Role = UserRoles.Admin,
            IsEmailVerified = true,
            MustChangePassword = true,
            CreatedBy = "seed",
            ModifiedBy = "seed"
        };

        await userRepo.CreateAsync(admin);
        _logger.LogInformation("Seeded admin user {Email} (id={UserId})", admin.Email, admin.UserId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
